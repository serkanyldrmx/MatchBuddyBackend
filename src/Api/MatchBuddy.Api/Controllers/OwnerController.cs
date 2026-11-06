using MatchBuddy.Api.Model;
using MatchBuddy.Api.Security;
using MatchBuddy.Api.Services;
using MatchBuddy.Business.Abstract;
using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MatchBuddy.Api.Controllers
{
    /// <summary>
    /// İşletme paneli (docs/venues-api.md). Her istek tesis sahipliği kontrolünden geçer (403);
    /// yazma uç noktaları üyelik aktif değilse 403 "Üyeliğiniz aktif değil." döner.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = Roles.Owner)]
    public class OwnerController : ControllerBase
    {
        private readonly IVenueService _venueService;
        private readonly IMediaService _mediaService;

        public OwnerController(IVenueService venueService, IMediaService mediaService)
        {
            _venueService = venueService;
            _mediaService = mediaService;
        }

        private int Me => User.GetPlayerId();

        // Başarılıysa Data'yı (dizi / nesne) doğrudan döner
        private IActionResult Data<T>(OperationResult<T> result) => this.ToHttp(result, result.Success ? result.Data : null);

        // Kayıt uç noktaları: { success, message, <idName>: id }
        private IActionResult Saved(OperationResult<int> result, string idName) =>
            this.ToHttp(result, result.Success
                ? new Dictionary<string, object> { ["success"] = true, ["message"] = result.Message, [idName] = result.Data }
                : null);

        [HttpGet("MyVenues")]
        public ActionResult<OwnerVenuesDto> MyVenues() => _venueService.MyVenues(Me);

        // Tesis bilgileri sayfası: oyunculara görünmese / üyelik aktif olmasa da çalışır
        [HttpGet("Venue")]
        public IActionResult Venue([FromQuery] int stadiumId) => Data(_venueService.OwnerVenue(Me, stadiumId));

        [HttpGet("Dashboard")]
        public IActionResult Dashboard([FromQuery] int? stadiumId) => Data(_venueService.Dashboard(Me, stadiumId));

        [HttpGet("Reservations")]
        public IActionResult Reservations([FromQuery] int? stadiumId, [FromQuery] byte? status, [FromQuery] DateTime? from, [FromQuery] DateTime? to) =>
            Data(_venueService.Reservations(Me, stadiumId, status, from, to));

        [HttpPost("RespondReservation")]
        public IActionResult RespondReservation(RespondReservationInput model) =>
            this.ToHttp(_venueService.RespondReservation(Me, model));

        [HttpGet("Calendar")]
        public IActionResult Calendar([FromQuery] int stadiumId, [FromQuery] DateTime? from, [FromQuery] DateTime? to) =>
            Data(_venueService.Calendar(Me, stadiumId, from, to));

        [HttpPost("Venue/Update")]
        public IActionResult UpdateVenue(VenueUpdateInput model) => this.ToHttp(_venueService.UpdateVenue(Me, model));

        // ---- Fotoğraflar

        [HttpGet("Photos")]
        public IActionResult Photos([FromQuery] int stadiumId) => Data(_venueService.Photos(Me, stadiumId));

        // Medya hattı (docs/media-api.md): kind venues, url = md URL
        [HttpPost("Photos/Upload")]
        [EnableRateLimiting(MediaUploadLimits.Policy)]
        [RequestSizeLimit(MediaUploadLimits.SingleRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MediaUploadLimits.SingleRequestBytes)]
        public async Task<IActionResult> UploadPhoto([FromForm] int stadiumId, [FromForm] IFormFile? file, CancellationToken ct)
        {
            var permission = _venueService.CheckCanAddPhoto(Me, stadiumId);
            if (!permission.Success) return this.ToHttp(permission);

            if (file == null) return BadRequest(new { Success = false, Message = "Fotoğraf seçilmedi." });
            var upload = await _mediaService.UploadAsync(file, MediaKind.VenuePhoto, Me, ct);
            if (!upload.Success) return this.ToHttp(upload);

            var result = _venueService.AddPhoto(Me, stadiumId, upload.Data.Url);
            if (!result.Success)
            {
                await _mediaService.DeleteAsync(upload.Data.MediaId, CancellationToken.None);
                return this.ToHttp(result);
            }
            _mediaService.LinkStadiumPhoto(result.Data.PhotoId, upload.Data.MediaId);
            return Ok(new { Success = true, result.Message, result.Data.PhotoId, result.Data.Url, result.Data.IsCover });
        }

        [HttpPost("Photos/Delete")]
        public async Task<IActionResult> DeletePhoto(PhotoIdInput model)
        {
            var mediaId = _mediaService.GetStadiumPhotoMediaId(model.PhotoId);
            var result = _venueService.DeletePhoto(Me, model.PhotoId);
            if (result.Success) await _mediaService.DeleteImageAsync(mediaId, result.Data, CancellationToken.None);
            return this.ToHttp(result, result.Success ? new { Success = true, result.Message } : null);
        }

        [HttpPost("Photos/SetCover")]
        public IActionResult SetCover(PhotoIdInput model) => this.ToHttp(_venueService.SetCover(Me, model.PhotoId));

        // ---- Sahalar

        [HttpGet("Pitches")]
        public IActionResult Pitches([FromQuery] int stadiumId) => Data(_venueService.Pitches(Me, stadiumId));

        [HttpPost("Pitches/Save")]
        public IActionResult SavePitch(PitchSaveInput model) => Saved(_venueService.SavePitch(Me, model), "pitchId");

        [HttpPost("Pitches/Delete")]
        public IActionResult DeletePitch(PitchIdInput model) => this.ToHttp(_venueService.DeletePitch(Me, model.PitchId));

        // ---- Açılış saatleri

        [HttpGet("OpeningHours")]
        public IActionResult OpeningHours([FromQuery] int stadiumId) => Data(_venueService.OpeningHours(Me, stadiumId));

        [HttpPost("OpeningHours")]
        public IActionResult SaveOpeningHours(OpeningHoursInput model) => this.ToHttp(_venueService.SaveOpeningHours(Me, model));

        // ---- Fiyat kuralları

        [HttpGet("PriceRules")]
        public IActionResult PriceRules([FromQuery] int stadiumId) => Data(_venueService.PriceRules(Me, stadiumId));

        [HttpPost("PriceRules/Save")]
        public IActionResult SavePriceRule(PriceRuleDto model) => Saved(_venueService.SavePriceRule(Me, model), "priceRuleId");

        [HttpPost("PriceRules/Delete")]
        public IActionResult DeletePriceRule(PriceRuleIdInput model) => this.ToHttp(_venueService.DeletePriceRule(Me, model.PriceRuleId));

        // ---- Kampanyalar

        [HttpGet("Campaigns")]
        public IActionResult Campaigns([FromQuery] int stadiumId) => Data(_venueService.Campaigns(Me, stadiumId));

        [HttpPost("Campaigns/Save")]
        public IActionResult SaveCampaign(CampaignSaveInput model) => Saved(_venueService.SaveCampaign(Me, model), "campaignId");

        [HttpPost("Campaigns/Delete")]
        public IActionResult DeleteCampaign(CampaignIdInput model) => this.ToHttp(_venueService.DeleteCampaign(Me, model.CampaignId));

        // ---- Bloklar

        [HttpGet("Blocks")]
        public IActionResult Blocks([FromQuery] int stadiumId, [FromQuery] DateTime? from, [FromQuery] DateTime? to) =>
            Data(_venueService.Blocks(Me, stadiumId, from, to));

        [HttpPost("Blocks/Save")]
        public IActionResult SaveBlock(BlockSaveInput model) => Saved(_venueService.SaveBlock(Me, model), "slotBlockId");

        [HttpPost("Blocks/Delete")]
        public IActionResult DeleteBlock(BlockIdInput model) => this.ToHttp(_venueService.DeleteBlock(Me, model.SlotBlockId));

        // Harici rezervasyonun ödeme durumu
        [HttpPost("Blocks/SetPaid")]
        public IActionResult SetBlockPaid(BlockPaidInput model) => this.ToHttp(_venueService.SetBlockPaid(Me, model));

        // Harici rezervasyon formu için saha ücreti (tesis görünür olmasa da çalışır): { pitchTotal, lines }
        [HttpPost("Quote")]
        public IActionResult Quote(OwnerQuoteInput model) => Data(_venueService.OwnerQuote(Me, model));

        // ---- Giderler ve finans

        [HttpGet("Expenses")]
        public IActionResult Expenses([FromQuery] int stadiumId, [FromQuery] DateTime? from, [FromQuery] DateTime? to) =>
            Data(_venueService.Expenses(Me, stadiumId, from, to));

        [HttpPost("Expenses/Save")]
        public IActionResult SaveExpense(ExpenseSaveInput model) => Saved(_venueService.SaveExpense(Me, model), "expenseId");

        [HttpPost("Expenses/Delete")]
        public IActionResult DeleteExpense(ExpenseIdInput model) => this.ToHttp(_venueService.DeleteExpense(Me, model.ExpenseId));

        [HttpGet("Finance")]
        public IActionResult Finance([FromQuery] int? stadiumId, [FromQuery] int? year, [FromQuery] int? month) =>
            Data(_venueService.Finance(Me, stadiumId, year, month));

        // ---- Yorumlar

        [HttpGet("Reviews")]
        public IActionResult Reviews([FromQuery] int stadiumId) => Data(_venueService.OwnerReviews(Me, stadiumId));

        [HttpPost("Reviews/Reply")]
        public IActionResult ReplyReview(ReviewReplyInput model) => this.ToHttp(_venueService.ReplyReview(Me, model));
    }
}
