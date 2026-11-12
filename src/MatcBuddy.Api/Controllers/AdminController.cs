using System.Text.Json;
using MatcBuddy.Api.Model;
using MatcBuddy.Api.Security;
using MatcBuddy.Api.Services;
using MatcBuddy.Business.Abstract;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatcBuddy.Api.Controllers
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
        private readonly IMediaService _mediaService;

        private readonly MatcBuddy.Business.Auth.IAuthService _auth;

        public AdminController(ISocialService socialService, IVenueService venueService, OsmService osmService, IMediaService mediaService,
            MatcBuddy.Business.Auth.IAuthService auth)
        {
            _auth = auth;
            _mediaService = mediaService;
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

        // ------------------------------------------------------------------ Harici saha satış listesi (docs/engagement-api.md §4)

        /// <summary>Oyuncuların telefonla ayarladığı, anlaşmamız olmayan sahalar: en çok maç yapılan önce.</summary>
        [HttpGet("ExternalVenueLeads")]
        public ActionResult<List<ExternalVenueLeadDto>> ExternalVenueLeads([FromQuery] string? q) => _venueService.ExternalLeads(q);

        /// <summary>Bağlanabilecek tesisler (harici olmayan; işletmeli ya da aday).</summary>
        [HttpGet("ExternalVenueLeads/Targets")]
        public ActionResult<List<LinkTargetDto>> ExternalVenueLinkTargets([FromQuery] string? q) => _venueService.LinkTargets(q);

        /// <summary>Harici sahaları mevcut bir tesise ya da yeni aday tesise bağla; maçları taşınır. Yanıt: { success, message, stadiumId }.</summary>
        [HttpPost("ExternalVenueLeads/Link")]
        public IActionResult LinkExternalVenues(LinkExternalInput input)
        {
            var result = _venueService.LinkExternal(input);
            return result.Success ? Ok(new { Success = true, result.Message, StadiumId = result.Data }) : this.ToHttp(result);
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
            // Şifre değişti: işletmenin açık oturumları (refresh token) kapanır
            if (res.Success) _auth.RevokeAllSessions(model.OwnerId, MatcBuddy.Entities.Entity.RevokeReasons.PasswordReset);
            return res.Success
                ? Ok(new { success = true, userName = res.Data!.UserName, password = res.Data.Password })
                : this.ToHttp(res);
        }

        // multipart: model (JSON) + photos (1–10 dosya). Hata: 400 { success:false, message, field }
        [HttpPost("CreateOwner")]
        [RequestSizeLimit(110 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 110 * 1024 * 1024)]
        public async Task<IActionResult> CreateOwner([FromForm] string? model, [FromForm] List<IFormFile>? photos, CancellationToken ct)
        {
            CreateOwnerInput? input;
            try
            {
                input = string.IsNullOrWhiteSpace(model) ? null : JsonSerializer.Deserialize<CreateOwnerInput>(model,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                return this.FieldError("model", ErrorCodes.Admin.InvalidOwnerModel);
            }

            photos ??= new List<IFormFile>();
            var invalid = _venueService.ValidateCreateOwner(input, photos.Count);
            if (invalid != null) return this.FieldError(invalid.Value.Field, invalid.Value.Error.Code, invalid.Value.Error.Args);

            // Medya hattı (kind venues); yükleyen admin. Hata olursa yüklenenler geri silinir.
            var uploaded = new List<MediaDto>();
            foreach (var photo in photos)
            {
                var upload = await _mediaService.UploadAsync(photo, MediaKind.VenuePhoto, User.GetPlayerId(), ct);
                if (!upload.Success)
                {
                    foreach (var m in uploaded) await _mediaService.DeleteAsync(m.MediaId, CancellationToken.None);
                    return this.FieldError("photos", ErrorCodes.Admin.PhotoUploadFailed, photo.FileName, upload.Message);
                }
                uploaded.Add(upload.Data);
            }

            var result = _venueService.CreateOwner(input!, uploaded.Select(m => m.Url).ToList());
            if (!result.Success)
            {
                foreach (var m in uploaded) await _mediaService.DeleteAsync(m.MediaId, CancellationToken.None);
                return this.Error(StatusCodes.Status400BadRequest, result.Code ?? ErrorCodes.Common.OperationFailed, result.Message,
                    new Dictionary<string, string[]> { ["owner.userName"] = new[] { result.Message } });
            }
            _mediaService.LinkStadiumPhotos(result.Data.StadiumId, uploaded);
            return Ok(new { Success = true, result.Message, result.Data.OwnerId, result.Data.StadiumId });
        }

        // Tek seferlik geçiş: /uploads/... profil ve tesis fotoğraflarını medya hattından geçirir.
        // İdempotent (ikinci çalıştırmada /uploads kaydı kalmaz); eski dosyalar silinmez.
        [HttpPost("Media/MigrateLegacy")]
        public async Task<IActionResult> MigrateLegacyMedia(CancellationToken ct)
        {
            var r = await _mediaService.MigrateLegacyAsync(ct);
            return Ok(new { Success = true, r.MigratedAvatars, r.MigratedVenuePhotos, r.Missing });
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
                    var maxDeg = (double)P.Dec(ParameterKeys.Osm.MaxBoxDegrees);
                    if (south >= north || west >= east || north - south > maxDeg || east - west > maxDeg)
                        return this.BadRequestError(ErrorCodes.Osm.InvalidBounds, maxDeg);
                    box = new OsmService.BBox(south.Value, west.Value, north.Value, east.Value);
                }
                else if (!string.IsNullOrWhiteSpace(city))
                {
                    box = await _osmService.CityBoundsAsync(city, ct);
                    if (box == null) return this.NotFoundError(ErrorCodes.Osm.CityNotFound);
                }
                else
                {
                    return this.BadRequestError(ErrorCodes.Osm.AreaRequired);
                }

                var candidates = await _osmService.PitchesAsync(box, ct);
                OsmService.MarkExisting(candidates, _venueService.GeoRefs());
                return Ok(candidates);
            }
            catch (Exception ex) when (ex is OsmService.OsmUnavailableException or FormatException or KeyNotFoundException or InvalidOperationException)
            {
                return ex is OsmService.OsmUnavailableException osm
                    ? this.Error(StatusCodes.Status502BadGateway, osm.Code, osm.Message)
                    : this.Error(StatusCodes.Status502BadGateway, ErrorCodes.Osm.InvalidResponse, Msg.Get(ErrorCodes.Osm.InvalidResponse));
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
