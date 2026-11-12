using System.Globalization;
using System.Text.Json;
using MatcBuddy.DataAccess.Concrete;
using MatcBuddy.Entities.DTOs;

namespace MatcBuddy.Api.Services
{
    /// <summary>
    /// OpenStreetMap: şehir sınırı (Nominatim) ve futbol sahası adayları (Overpass). Hata / zaman aşımı
    /// OsmUnavailableException fırlatır (kontrolcü 502 döner).
    /// </summary>
    public class OsmService
    {
        public const string HttpClientName = "osm";
        private const string NominatimUrl = "https://nominatim.openstreetmap.org/search";

        /// <summary>
        /// Overpass aynaları. Ana sunucu (overpass-api.de) yoğunken isteği uzun süre kuyrukta bekletebiliyor
        /// ya da reddedebiliyor; bu yüzden aynalara paralel istek atılır ve ilk başarılı yanıt kullanılır.
        /// appsettings: "Osm:OverpassUrls": [ ... ] ile değiştirilebilir.
        /// </summary>
        private static readonly string[] DefaultOverpassUrls =
        {
            "https://overpass-api.de/api/interpreter",
            "https://maps.mail.ru/osm/tools/overpass/api/interpreter",
            "https://overpass.kumi.systems/api/interpreter",
            "https://overpass.private.coffee/api/interpreter",
        };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OsmService> _logger;
        private readonly string[] _overpassUrls;

        public OsmService(IHttpClientFactory httpClientFactory, ILogger<OsmService> logger, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            var configured = configuration.GetSection("Osm:OverpassUrls").Get<string[]>();
            _overpassUrls = configured is { Length: > 0 } ? configured : DefaultOverpassUrls;
        }

        /// <summary>Harita servisi hatası. Code = ErrorCodes.Osm.*; Message istek dilinde çözülmüş metindir.</summary>
        public class OsmUnavailableException : Exception
        {
            public OsmUnavailableException(string code, Exception? inner = null, params object?[] args)
                : base(Msg.Get(code, args), inner)
            {
                Code = code;
            }

            public string Code { get; }
        }

        public record BBox(double South, double West, double North, double East);

        public async Task<BBox?> CityBoundsAsync(string city, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var url = $"{NominatimUrl}?format=json&limit=1&q={Uri.EscapeDataString(city.Trim() + ", Türkiye")}";
            using var doc = await GetJsonAsync(client, () => new HttpRequestMessage(HttpMethod.Get, url), ct);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0) return null;
            var bb = doc.RootElement[0].GetProperty("boundingbox"); // [south, north, west, east] (metin)
            double P(int i) => double.Parse(bb[i].GetString()!, CultureInfo.InvariantCulture);
            return new BBox(P(0), P(2), P(1), P(3));
        }

        public async Task<List<OsmCandidateDto>> PitchesAsync(BBox box, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var b = string.Create(CultureInfo.InvariantCulture, $"{box.South},{box.West},{box.North},{box.East}");
            var query = $"[out:json][timeout:{P.Int(ParameterKeys.Osm.OverpassTimeoutSeconds)}];(" +
                        $"node[\"leisure\"=\"pitch\"][\"sport\"~\"soccer\"]({b});" +
                        $"way[\"leisure\"=\"pitch\"][\"sport\"~\"soccer\"]({b});" +
                        $");out center tags {P.Int(ParameterKeys.Osm.MaxResults)};";
            using var doc = await FirstOverpassResponseAsync(client, query, ct);

            var result = new List<OsmCandidateDto>();
            if (!doc.RootElement.TryGetProperty("elements", out var elements)) return result;
            foreach (var e in elements.EnumerateArray())
            {
                var type = e.GetProperty("type").GetString();
                double lat, lon;
                if (type == "node")
                {
                    lat = e.GetProperty("lat").GetDouble();
                    lon = e.GetProperty("lon").GetDouble();
                }
                else if (e.TryGetProperty("center", out var center))
                {
                    lat = center.GetProperty("lat").GetDouble();
                    lon = center.GetProperty("lon").GetDouble();
                }
                else continue;

                var tags = e.TryGetProperty("tags", out var t) ? t : default;
                string? Tag(params string[] keys)
                {
                    if (tags.ValueKind != JsonValueKind.Object) return null;
                    foreach (var k in keys)
                    {
                        if (tags.TryGetProperty(k, out var v) && !string.IsNullOrWhiteSpace(v.GetString())) return v.GetString();
                    }
                    return null;
                }

                var street = string.Join(" ", new[] { Tag("addr:street"), Tag("addr:housenumber") }.Where(x => x != null));
                var address = string.Join(", ", new[] { street, Tag("addr:district", "addr:suburb"), Tag("addr:city") }
                    .Where(x => !string.IsNullOrWhiteSpace(x)));
                var id = e.GetProperty("id").GetInt64();
                result.Add(new OsmCandidateDto
                {
                    OsmId = id,
                    Name = Tag("name") ?? Msg.Get(MessageKeys.Osm.LabelUnnamedPitch),
                    Lat = Math.Round(lat, 6),
                    Lon = Math.Round(lon, 6),
                    Address = string.IsNullOrWhiteSpace(address) ? null : address,
                    Phone = Tag("phone", "contact:phone"),
                    Website = Tag("website", "contact:website"),
                    OpeningHours = Tag("opening_hours"),
                });
            }
            return result;
        }

        /// <summary>alreadyExists: aynı OsmId ya da osm.duplicateRadiusMeters (varsayılan 150 m) içinde bir tesis.</summary>
        public static void MarkExisting(List<OsmCandidateDto> candidates, List<VenueGeoRefDto> venues)
        {
            var osmIds = venues.Where(v => v.OsmId.HasValue).Select(v => v.OsmId!.Value).ToHashSet();
            var points = venues
                .Select(v => VenueGeo.TryParseLocation(v.Location, out var la, out var lo) ? (la, lo) : ((double, double)?)null)
                .Where(p => p.HasValue).Select(p => p!.Value).ToList();
            var radius = P.Int(ParameterKeys.Osm.DuplicateRadiusMeters);
            foreach (var c in candidates)
            {
                c.AlreadyExists = osmIds.Contains(c.OsmId)
                                  || points.Any(p => VenueGeo.DistanceMeters(p.Item1, p.Item2, c.Lat, c.Lon) <= radius);
            }
        }

        /// <summary>Sorguyu tüm Overpass aynalarına aynı anda gönderir; ilk geçerli JSON yanıtı döner, diğerleri iptal edilir.</summary>
        private async Task<JsonDocument> FirstOverpassResponseAsync(HttpClient client, string query, CancellationToken ct)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var pending = _overpassUrls.Select(url => TryOverpassAsync(client, url, query, linked.Token)).ToList();
            while (pending.Count > 0)
            {
                var done = await Task.WhenAny(pending);
                pending.Remove(done);
                var doc = await done;
                if (doc != null)
                {
                    linked.Cancel(); // kalan istekleri bırak
                    return doc;
                }
            }
            ct.ThrowIfCancellationRequested();
            throw new OsmUnavailableException(ErrorCodes.Osm.AllMirrorsFailed);
        }

        private async Task<JsonDocument?> TryOverpassAsync(HttpClient client, string url, string query, CancellationToken ct)
        {
            var started = DateTime.UtcNow;
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["data"] = query }),
                };
                req.Headers.Accept.ParseAdd("application/json");
                using var resp = await client.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Overpass {Url} {Status} döndü", url, (int)resp.StatusCode);
                    return null;
                }
                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                if (!doc.RootElement.TryGetProperty("elements", out _))
                {
                    doc.Dispose();
                    return null;
                }
                _logger.LogInformation("Overpass yanıtı {Url} üzerinden {Ms} ms'de alındı", url, (int)(DateTime.UtcNow - started).TotalMilliseconds);
                return doc;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException)
            {
                if (!ct.IsCancellationRequested)
                    _logger.LogInformation("Overpass {Url} başarısız: {Message}", url, ex.Message);
                return null;
            }
        }

        private async Task<JsonDocument> GetJsonAsync(HttpClient client, Func<HttpRequestMessage> request, CancellationToken ct)
        {
            try
            {
                using var req = request();
                using var resp = await client.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    throw new OsmUnavailableException(ErrorCodes.Osm.ServiceError, null, (int)resp.StatusCode);
                }
                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            }
            catch (OsmUnavailableException)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "OSM isteği başarısız");
                throw new OsmUnavailableException(ErrorCodes.Osm.Unreachable, ex);
            }
        }
    }
}
