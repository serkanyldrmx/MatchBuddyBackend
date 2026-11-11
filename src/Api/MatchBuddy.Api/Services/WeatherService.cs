using System.Globalization;
using System.Text.Json;
using MatchBuddy.DataAccess;
using MatchBuddy.DataAccess.Concrete;
using MatchBuddy.Entities.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace MatchBuddy.Api.Services
{
    /// <summary>
    /// Açık sahadaki maçlar için hava durumu (docs/engagement-api.md §2). Kaynak: Open-Meteo (ücretsiz, anahtarsız).
    /// Saatlik tahmin konum başına (0,01° yuvarlanmış) 1 saat önbellekte tutulur; konumu olmayan sahada ilçe/şehir
    /// adı Open-Meteo geocoding ile çözülür (7 gün önbellek). Kapalı saha (Pitch.IsIndoor) için tahmin istenmez.
    /// Servise ulaşılamazsa maç "available: false" döner; ekran bozulmaz.
    /// </summary>
    public class WeatherService
    {
        public const string HttpClientName = "weather";
        /// <summary>Open-Meteo saatlik tahmin ufku (gün)</summary>
        public const int HorizonDays = 15;
        private const int MaxMatches = 60;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly ILogger<WeatherService> _logger;
        private readonly string _timeZone;

        public WeatherService(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<WeatherService> logger, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _logger = logger;
            // Maç saatleri sunucu (Türkiye) saatiyle saklanır
            _timeZone = configuration["Weather:TimeZone"] ?? "Europe/Istanbul";
        }

        private sealed record Forecast(DateTime[] Times, double?[] Temp, double?[] Apparent, int?[] PrecipProb, double?[] Precip, int?[] Code, double?[] Wind);

        public async Task<List<MatchWeatherDto>> ForMatchesAsync(IEnumerable<int> matchIds, CancellationToken ct)
        {
            var ids = matchIds.Where(i => i > 0).Distinct().Take(MaxMatches).ToList();
            if (ids.Count == 0) return new List<MatchWeatherDto>();

            using var context = new MatchBuddyContext();
            var rows = await (from m in context.Matchs.AsNoTracking()
                              join s in context.Stadiums.AsNoTracking() on m.StadiumId equals s.StadiumId
                              join p in context.Pitches.AsNoTracking() on m.PitchId equals p.PitchId into pj
                              from p in pj.DefaultIfEmpty()
                              where ids.Contains(m.MatchId)
                              select new { m.MatchId, m.MatchDate, m.DurationMinutes, s.Location, s.City, s.District, Indoor = p != null && p.IsIndoor })
                .ToListAsync(ct);

            var now = DateTime.Now;
            var result = new List<MatchWeatherDto>();
            foreach (var r in rows)
            {
                var dto = new MatchWeatherDto { MatchId = r.MatchId, Indoor = r.Indoor };
                result.Add(dto);
                if (r.Indoor || r.MatchDate == null) continue;
                var start = r.MatchDate.Value;
                var end = start.AddMinutes(r.DurationMinutes <= 0 ? 60 : r.DurationMinutes);
                if (end < now || start > now.AddDays(HorizonDays)) continue;

                double lat, lon;
                if (VenueGeo.TryParseLocation(r.Location, out lat, out lon))
                {
                    dto.Approximate = false;
                }
                else
                {
                    var geo = await GeocodeAsync(r.District, r.City, ct);
                    if (geo == null) continue;
                    (lat, lon) = geo.Value;
                    dto.Approximate = true;
                }

                var forecast = await ForecastAsync(lat, lon, ct);
                if (forecast == null) continue;
                // Maçın başladığı saat (dakikalar atılır); maç başladıysa şimdiki saat
                var at = (start < now ? now : start);
                var hour = new DateTime(at.Year, at.Month, at.Day, at.Hour, 0, 0);
                var i = Array.IndexOf(forecast.Times, hour);
                if (i < 0) continue;
                dto.Available = true;
                dto.Time = hour;
                dto.Temperature = forecast.Temp[i];
                dto.ApparentTemperature = forecast.Apparent[i];
                dto.PrecipitationProbability = forecast.PrecipProb[i];
                dto.Precipitation = forecast.Precip[i];
                dto.WeatherCode = forecast.Code[i];
                dto.WindSpeed = forecast.Wind[i];
            }
            return result;
        }

        private async Task<Forecast?> ForecastAsync(double lat, double lon, CancellationToken ct)
        {
            lat = Math.Round(lat, 2);
            lon = Math.Round(lon, 2);
            var key = $"weather:{lat.ToString(CultureInfo.InvariantCulture)},{lon.ToString(CultureInfo.InvariantCulture)}";
            if (_cache.TryGetValue(key, out Forecast? cached)) return cached;

            var url = string.Create(CultureInfo.InvariantCulture,
                $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}" +
                $"&hourly=temperature_2m,apparent_temperature,precipitation_probability,precipitation,weather_code,wind_speed_10m" +
                $"&timezone={Uri.EscapeDataString(_timeZone)}&forecast_days=16");
            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                using var doc = JsonDocument.Parse(await client.GetStringAsync(url, ct));
                var h = doc.RootElement.GetProperty("hourly");
                var times = h.GetProperty("time").EnumerateArray()
                    .Select(t => DateTime.ParseExact(t.GetString()!, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture)).ToArray();
                double?[] D(string name) => h.TryGetProperty(name, out var a)
                    ? a.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.Number ? v.GetDouble() : (double?)null).ToArray()
                    : new double?[times.Length];
                int?[] I(string name) => D(name).Select(v => v.HasValue ? (int?)(int)Math.Round(v.Value) : null).ToArray();
                var forecast = new Forecast(times, D("temperature_2m"), D("apparent_temperature"), I("precipitation_probability"),
                    D("precipitation"), I("weather_code"), D("wind_speed_10m"));
                _cache.Set(key, forecast, TimeSpan.FromHours(1));
                return forecast;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Hava durumu alınamadı ({Lat}, {Lon}).", lat, lon);
                // Kısa süre tekrar denenmesin
                _cache.Set(key, (Forecast?)null, TimeSpan.FromMinutes(5));
                return null;
            }
        }

        private async Task<(double, double)?> GeocodeAsync(string? district, string? city, CancellationToken ct)
        {
            var name = !string.IsNullOrWhiteSpace(district) ? district!.Trim() : city?.Trim();
            if (string.IsNullOrWhiteSpace(name)) return null;
            var key = $"geocode:{name.ToLowerInvariant()}|{city?.Trim().ToLowerInvariant()}";
            if (_cache.TryGetValue(key, out (double, double)? cached)) return cached;
            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(name)}&count=5&language=tr&countryCode=TR";
                using var doc = JsonDocument.Parse(await client.GetStringAsync(url, ct));
                (double, double)? found = null;
                if (doc.RootElement.TryGetProperty("results", out var results))
                {
                    // İlçe adı birden çok ilde olabilir: şehir adı eşleşeni tercih et
                    var list = results.EnumerateArray().ToList();
                    var match = list.FirstOrDefault(r => city != null && r.TryGetProperty("admin1", out var a1)
                                                         && string.Equals(a1.GetString(), city.Trim(), StringComparison.CurrentCultureIgnoreCase));
                    var pick = match.ValueKind == JsonValueKind.Object ? match : list.FirstOrDefault();
                    if (pick.ValueKind == JsonValueKind.Object)
                        found = (pick.GetProperty("latitude").GetDouble(), pick.GetProperty("longitude").GetDouble());
                }
                _cache.Set(key, found, TimeSpan.FromDays(7));
                return found;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Konum çözülemedi ({Name}).", name);
                return null;
            }
        }
    }
}
