using System.Globalization;
using System.Text.Json;
using MatchBuddy.DataAccess.Concrete;
using MatchBuddy.Entities.DTOs;

namespace MatchBuddy.Api.Services
{
    /// <summary>
    /// OpenStreetMap: şehir sınırı (Nominatim) ve futbol sahası adayları (Overpass). Hata / zaman aşımı
    /// OsmUnavailableException fırlatır (kontrolcü 502 döner).
    /// </summary>
    public class OsmService
    {
        public const string HttpClientName = "osm";
        private const string OverpassUrl = "https://overpass-api.de/api/interpreter";
        private const string NominatimUrl = "https://nominatim.openstreetmap.org/search";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OsmService> _logger;

        public OsmService(IHttpClientFactory httpClientFactory, ILogger<OsmService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public class OsmUnavailableException : Exception
        {
            public OsmUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
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
            var query = "[out:json][timeout:25];(" +
                        $"node[\"leisure\"=\"pitch\"][\"sport\"~\"soccer\"]({b});" +
                        $"way[\"leisure\"=\"pitch\"][\"sport\"~\"soccer\"]({b});" +
                        ");out center tags 200;";
            using var doc = await GetJsonAsync(client, () => new HttpRequestMessage(HttpMethod.Post, OverpassUrl)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["data"] = query }),
            }, ct);

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
                    Name = Tag("name") ?? "Adsız halı saha",
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

        /// <summary>alreadyExists: aynı OsmId ya da 150 m içinde bir tesis.</summary>
        public static void MarkExisting(List<OsmCandidateDto> candidates, List<VenueGeoRefDto> venues)
        {
            var osmIds = venues.Where(v => v.OsmId.HasValue).Select(v => v.OsmId!.Value).ToHashSet();
            var points = venues
                .Select(v => VenueGeo.TryParseLocation(v.Location, out var la, out var lo) ? (la, lo) : ((double, double)?)null)
                .Where(p => p.HasValue).Select(p => p!.Value).ToList();
            foreach (var c in candidates)
            {
                c.AlreadyExists = osmIds.Contains(c.OsmId)
                                  || points.Any(p => VenueGeo.DistanceMeters(p.Item1, p.Item2, c.Lat, c.Lon) <= 150);
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
                    throw new OsmUnavailableException($"Harita servisi hata döndü ({(int)resp.StatusCode}).");
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
                throw new OsmUnavailableException("Harita servisine ulaşılamadı. Lütfen daha sonra tekrar deneyin.", ex);
            }
        }
    }
}
