using MatchBuddy.Api.Services;
using MatchBuddy.Entities.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatchBuddy.Api.Controllers
{
    /// <summary>
    /// Maç davet linki — herkese açık uçlar (docs/engagement-api.md §3):
    ///  GET /m/{kod}, /share/m/{kod}  → Open Graph önizleme sayfası + ön yüze yönlendirme (WhatsApp kartı)
    ///  GET /api/Public/Match/{kod}   → davet sayfası için maç özeti (giriş gerekmez)
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    public class ShareController : ControllerBase
    {
        private readonly ShareService _share;

        public ShareController(ShareService share)
        {
            _share = share;
        }

        [HttpGet("m/{code}")]
        [HttpGet("share/m/{code}")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public ContentResult Preview(string code)
        {
            var invite = _share.Invite(code);
            Response.Headers.CacheControl = "public, max-age=300";
            return Content(_share.PreviewHtml(code, invite, _share.DefaultImageUrl), "text/html; charset=utf-8");
        }

        [HttpGet("api/Public/Match/{code}")]
        public ActionResult<PublicMatchInviteDto> Match(string code)
        {
            var invite = _share.Invite(code);
            return invite == null ? this.NotFoundError(ErrorCodes.Share.NotFound) : invite;
        }
    }
}
