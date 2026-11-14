using MatcBuddy.Api.Model;
using MatcBuddy.Api.Security;
using MatcBuddy.Business.Abstract;
using MatcBuddy.Entities.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatcBuddy.Api.Controllers
{
    /// <summary>Anlaşmalı tesisler — oyuncu uç noktaları (docs/venues-api.md).</summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class VenuesController : ControllerBase
    {
        private readonly IVenueService _venueService;

        public VenuesController(IVenueService venueService)
        {
            _venueService = venueService;
        }

        // date verilirse freeSlotsToday o gün için hesaplanır
        [HttpGet("List")]
        public ActionResult<List<VenueListItemDto>> List([FromQuery] string? city, [FromQuery] string? q, [FromQuery] DateTime? date)
        {
            return _venueService.List(city, q, date);
        }

        [HttpGet("Detail")]
        public IActionResult Detail([FromQuery] int stadiumId)
        {
            var result = _venueService.Detail(stadiumId, User.GetPlayerId(), User.IsAdmin());
            return this.ToHttp(result, result.Success ? result.Data : null);
        }

        [HttpGet("Availability")]
        public IActionResult Availability([FromQuery] int stadiumId, [FromQuery] DateTime? date)
        {
            var result = _venueService.Availability(stadiumId, date, User.GetPlayerId(), User.IsAdmin());
            return this.ToHttp(result, result.Success ? result.Data : null);
        }

        [HttpPost("Quote")]
        public IActionResult Quote(QuoteInput model)
        {
            var result = _venueService.Quote(model);
            return this.ToHttp(result, result.Success ? result.Data : null);
        }

        [HttpPost("Review")]
        public IActionResult Review(ReviewInput model)
        {
            return this.ToHttp(_venueService.Review(User.GetPlayerId(), model));
        }
    }
}
