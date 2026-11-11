using MatchBuddy.Api.Model;
using MatchBuddy.Api.Security;
using MatchBuddy.Api.Services;
using MatchBuddy.Business.Abstract;
using MatchBuddy.Entities.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatchBuddy.Api.Controllers
{
    /// <summary>Sosyal paket (docs/social-api.md): takip, keşfet, profil, engel, şikayet, ayarlar, akış.</summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SocialController : ControllerBase
    {
        private readonly ISocialService _socialService;
        private readonly IMediaService _mediaService;

        public SocialController(ISocialService socialService, IMediaService mediaService)
        {
            _socialService = socialService;
            _mediaService = mediaService;
        }

        [HttpPost("Follow")]
        public IActionResult Follow(PlayerIdModel model)
        {
            var result = _socialService.Follow(User.GetPlayerId(), model.PlayerId);
            return this.ToHttp(result, new { Success = true, result.Message, IsMutual = result.Data });
        }

        [HttpPost("Unfollow")]
        public IActionResult Unfollow(PlayerIdModel model)
        {
            return this.ToHttp(_socialService.Unfollow(User.GetPlayerId(), model.PlayerId));
        }

        // playerId boş = ben
        [HttpGet("Followers")]
        public ActionResult<List<SocialPlayerDto>> Followers([FromQuery] int? playerId)
        {
            var result = _socialService.GetFollowers(User.GetPlayerId(), playerId);
            if (!result.Success) return this.ToHttp(result);
            return result.Data;
        }

        [HttpGet("Following")]
        public ActionResult<List<SocialPlayerDto>> Following([FromQuery] int? playerId)
        {
            var result = _socialService.GetFollowing(User.GetPlayerId(), playerId);
            if (!result.Success) return this.ToHttp(result);
            return result.Data;
        }

        [HttpGet("Discover")]
        public ActionResult<List<SocialPlayerDto>> Discover([FromQuery] string? q, [FromQuery] int take = 30)
        {
            return _socialService.Discover(User.GetPlayerId(), q, take);
        }

        [HttpGet("Suggestions")]
        public ActionResult<List<SocialPlayerDto>> Suggestions([FromQuery] int take = 10)
        {
            return _socialService.Suggestions(User.GetPlayerId(), take);
        }

        [HttpGet("Profile")]
        public ActionResult<PlayerProfileDto> Profile([FromQuery] int? playerId)
        {
            var result = _socialService.GetProfile(User.GetPlayerId(), playerId);
            if (!result.Success) return this.ToHttp(result);
            return result.Data;
        }

        [HttpPost("Block")]
        public IActionResult Block(PlayerIdModel model)
        {
            return this.ToHttp(_socialService.Block(User.GetPlayerId(), model.PlayerId));
        }

        [HttpPost("Unblock")]
        public IActionResult Unblock(PlayerIdModel model)
        {
            return this.ToHttp(_socialService.Unblock(User.GetPlayerId(), model.PlayerId));
        }

        [HttpGet("Blocked")]
        public ActionResult<List<PlayerSummaryDto>> Blocked()
        {
            return _socialService.GetBlocked(User.GetPlayerId());
        }

        [HttpPost("Report")]
        public IActionResult Report(ReportModel model)
        {
            return this.ToHttp(_socialService.Report(User.GetPlayerId(), model.PlayerId, model.Reason, model.Details));
        }

        [HttpGet("Settings")]
        public ActionResult<PlayerSettingsDto> GetSettings()
        {
            return _socialService.GetSettings(User.GetPlayerId());
        }

        [HttpPost("Settings")]
        public IActionResult SaveSettings(PlayerSettingsInput model)
        {
            return this.ToHttp(_socialService.SaveSettings(User.GetPlayerId(), model));
        }

        [HttpPost("DeleteAccount")]
        public async Task<IActionResult> DeleteAccount(DeleteAccountModel model)
        {
            var me = User.GetPlayerId();
            var (avatarMediaId, avatarUrl) = _mediaService.GetAvatar(me);
            var result = _socialService.DeleteAccount(me, model.Password);
            // Hesap silindiyse profil fotoğrafı (medya ya da eski /uploads dosyası) da silinir
            if (result.Success) await _mediaService.DeleteImageAsync(avatarMediaId, avatarUrl, CancellationToken.None);
            return this.ToHttp(result);
        }

        [HttpGet("MessageContacts")]
        public ActionResult<List<MessageContactDto>> MessageContacts()
        {
            return _socialService.GetMessageContacts(User.GetPlayerId());
        }

        [HttpGet("Feed")]
        public ActionResult<List<ActivityDto>> Feed([FromQuery] int take = 30, [FromQuery] DateTime? before = null)
        {
            return _socialService.GetFeed(User.GetPlayerId(), take, before);
        }
    }
}
