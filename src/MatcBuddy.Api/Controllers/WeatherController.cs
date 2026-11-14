using MatcBuddy.Api.Services;
using MatcBuddy.Entities.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatcBuddy.Api.Controllers
{
    /// <summary>Maç saati hava durumu (docs/engagement-api.md §2).</summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class WeatherController : ControllerBase
    {
        private readonly WeatherService _weather;

        public WeatherController(WeatherService weather)
        {
            _weather = weather;
        }

        /// <summary>?ids=12,15,20 (en fazla 60). Kapalı sahada indoor: true, tahmin yoksa available: false.</summary>
        [HttpGet("Matches")]
        public async Task<ActionResult<List<MatchWeatherDto>>> Matches([FromQuery] string? ids, CancellationToken ct)
        {
            var list = (ids ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var id) ? id : 0)
                .Where(id => id > 0);
            return await _weather.ForMatchesAsync(list, ct);
        }
    }
}
