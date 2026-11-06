using MatchBuddy.Api.Model;
using MatchBuddy.Api.Security;
using MatchBuddy.Api.Services;
using MatchBuddy.Entities.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MatchBuddy.Api.Controllers
{
    /// <summary>Medya yükleme (docs/media-api.md §4). Şimdilik sadece mesaj resmi bu yoldan yüklenir.</summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [EnableRateLimiting(MediaUploadLimits.Policy)]
    public class MediaController : ControllerBase
    {
        private readonly IMediaService _mediaService;

        public MediaController(IMediaService mediaService)
        {
            _mediaService = mediaService;
        }

        // multipart: file, kind=message -> { success, mediaId, url, thumbUrl, largeUrl, width, height } (Pending)
        [HttpPost("Upload")]
        [RequestSizeLimit(MediaUploadLimits.SingleRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MediaUploadLimits.SingleRequestBytes)]
        public async Task<IActionResult> Upload([FromForm] IFormFile? file, [FromForm] string? kind, CancellationToken ct)
        {
            var k = kind?.Trim().ToLowerInvariant();
            if (k is not ("message" or "messages"))
            {
                return BadRequest(new { Success = false, Message = "Geçersiz medya türü (kind: message)." });
            }

            var result = await _mediaService.UploadAsync(file, MediaKind.MessageImage, User.GetPlayerId(), ct);
            if (!result.Success) return this.ToHttp(result);
            var m = result.Data;
            return Ok(new { Success = true, m.MediaId, m.Url, m.ThumbUrl, m.LargeUrl, m.Width, m.Height });
        }
    }

    public static class MediaUploadLimits
    {
        public const string Policy = "upload";
        /// <summary>10 MB dosya + multipart ek yükü. Sınırın üstü 413; 10 MB–12 MB arası uygulama tarafından 400.</summary>
        public const int SingleRequestBytes = 12 * 1024 * 1024;
    }
}
