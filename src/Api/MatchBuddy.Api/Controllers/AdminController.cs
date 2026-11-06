using System.Text.Json;
using MatchBuddy.Api.Model;
using MatchBuddy.Api.Security;
using MatchBuddy.Api.Services;
using MatchBuddy.Business.Abstract;
using MatchBuddy.Entities.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatchBuddy.Api.Controllers
{
    /// <summary>Admin: oyuncu şikayetleri, işletme hesapları ve tesisler (docs/venues-api.md).</summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = Roles.Admin)]
    public class AdminController : ControllerBase
    {
        private readonly ISocialService _socialService;
        private readonly IVenueService _venueService;
        private readonly OsmService _osmService;

        public AdminController(ISocialService socialService, IVenueService venueService, OsmService osmService)
        {
            _socialService = socialService;
            _venueService = venueService;
            _osmService = osmService;
        }

        // ?status=0 -> sadece yeni; boş -> hepsi
        [HttpGet("Reports")]
        public ActionResult<List<AdminReportDto>> Reports([FromQuery] byte? status)
        {
            return _socialService.GetReports(status);
        }

        [HttpPost("ResolveReport")]
        public IActionResult ResolveReport(ReportIdModel model)
        {
            return this.ToHttp(_socialService.ResolveReport(model.ReportId));
        }

        // ------------------------------------------------------------------ İşletmeler

        [HttpGet("Owners")]
        public ActionResult<List<AdminOwnerDto>> Owners() => _venueService.Owners();

        // Tek işletmenin tüm bilgileri (tesisler, fotoğraflar, istatistikler). Şifre dönmez.
        [HttpGet("OwnerDetail")]
        public IActionResult OwnerDetail([FromQuery] int ownerId)
        {
            var res = _venueService.OwnerDetail(ownerId);
            return res.Success ? Ok(res.Data) : this.ToHttp(res);
        }

        // Yeni geçici şifre üretir; düz metin şifre sadece bu yanıtta bir kez döner.
        [HttpPost("ResetOwnerPassword")]
        public IActionResult ResetOwnerPassword(ResetOwnerPasswordModel model)
        {
            var res = _venueService.ResetOwnerPassword(model.OwnerId);
            return res.Success
                ? Ok(new { success = true, userName = res.Data!.UserName, password = res.Data.Password })
                : this.ToHttp(res);
        }

        // multipart: model (JSON) + photos (1–10 dosya). Hata: 400 { success:false, message, field }
        [HttpPost("CreateOwner")]
        [RequestSizeLimit(60 * 1024 * 1024)]
        public IActionResult CreateOwner([FromForm] string? model, [FromForm] List<IFormFile>? photos)
        {
            CreateOwnerInput? input;
            try
            {
                input = string.IsNullOrWhiteSpace(model) ? null : JsonSerializer.Deserialize<CreateOwnerInput>(model,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                return BadRequest(new { Success = false, Message = "Geçersiz işletme bilgisi (model JSON).", Field = "model" });
            }

            photos ??= new List<IFormFile>();
            var invalid = _venueService.ValidateCreateOwner(input, photos.Count);
            if (invalid != null) return BadRequest(new { Success = false, Message = invalid.Value.Message, Field = invalid.Value.Field });

            foreach (var photo in photos)
            {
                var error = ImageUploads.Validate(photo);
                if (error != null) return BadRequest(new { Success = false, Message = $"{photo.FileName}: {error}", Field = "photos" });
            }

            var urls = photos.Select(p => ImageUploads.Save(p, "venues")).ToList();
            var result = _venueService.CreateOwner(input!, urls);
            if (!result.Success)
            {
                urls.ForEach(ImageUploads.Delete);
                return BadRequest(new { Success = false, result.Message, Field = "owner.userName" });
            }
            return Ok(new { Success = true, result.Message, result.Data.OwnerId, result.Data.StadiumId });
        }

        [HttpPost("UpdateMembership")]
        public IActionResult UpdateMembership(UpdateMembershipInput model) => this.ToHttp(_venueService.UpdateMembership(model));

        [HttpPost("AssignVenue")]
        public IActionResult AssignVenue(AssignVenueInput model) => this.ToHttp(_venueService.AssignVenue(model));

        [HttpPost("SetListed")]
        public IActionResult SetListed(SetListedInput model) => this.ToHttp(_venueService.SetListed(model));

        // ?city= ya da ?south=&west=&north=&east= ; hata / zaman aşımı -> 502
        [HttpGet("OsmCandidates")]
        public async Task<IActionResult> OsmCandidates([FromQuery] string? city, [FromQuery] double? south, [FromQuery] double? west,
            [FromQuery] double? north, [FromQuery] double? east, CancellationToken ct)
        {
            OsmService.BBox? box;
            try
            {
                if (south.HasValue && west.HasValue && north.HasValue && east.HasValue)
                {
                    if (south >= north || west >= east || north - south > 2 || east - west > 2)
                        return BadRequest(new { Success = false, Message = "Geçersiz alan (en fazla 2° x 2°, south < north, west < east)." });
                    box = new OsmService.BBox(south.Value, west.Value, north.Value, east.Value);
                }
                else if (!string.IsNullOrWhiteSpace(city))
                {
                    box = await _osmService.CityBoundsAsync(city, ct);
                    if (box == null) return NotFound(new { Success = false, Message = "Şehir bulunamadı." });
                }
                else
                {
                    return BadRequest(new { Success = false, Message = "city ya da south/west/north/east verilmelidir." });
                }

                var candidates = await _osmService.PitchesAsync(box, ct);
                OsmService.MarkExisting(candidates, _venueService.GeoRefs());
                return Ok(candidates);
            }
            catch (Exception ex) when (ex is OsmService.OsmUnavailableException or FormatException or KeyNotFoundException or InvalidOperationException)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    Success = false,
                    Message = ex is OsmService.OsmUnavailableException ? ex.Message : "Harita servisinden geçersiz yanıt alındı.",
                });
            }
        }

        [HttpPost("ImportCandidate")]
        public IActionResult ImportCandidate(ImportCandidateInput model)
        {
            var result = _venueService.ImportCandidate(model);
            return this.ToHttp(result, result.Success ? new { Success = true, result.Message, StadiumId = result.Data } : null);
        }
    }

    public class ResetOwnerPasswordModel
    {
        public int OwnerId { get; set; }
    }
}
