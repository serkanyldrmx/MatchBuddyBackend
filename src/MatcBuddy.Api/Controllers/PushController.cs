using MatcBuddy.Api.Infrastructure;
using MatcBuddy.Api.Security;
using MatcBuddy.Api.Services.Push;
using MatcBuddy.Core.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatcBuddy.Api.Controllers
{
    /// <summary>Telefona / tarayıcıya bildirim (Web Push) — docs/push-api.md.</summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PushController : ControllerBase
    {
        private readonly VapidKeys _keys;

        public PushController(VapidKeys keys) => _keys = keys;

        /// <summary>Tarayıcının abone olurken kullanacağı VAPID açık anahtarı.</summary>
        [HttpGet("PublicKey")]
        [AllowAnonymous]
        public IActionResult PublicKey() => Ok(new { PublicKey = _keys.PublicKey });

        /// <summary>Bu cihazı bildirimlere abone eder (aynı endpoint tekrar gelirse güncellenir).</summary>
        [HttpPost("Subscribe")]
        public IActionResult Subscribe(PushSubscribeModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Endpoint) || model.Endpoint.Length > 800
                || !Uri.TryCreate(model.Endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
                || string.IsNullOrWhiteSpace(model.Keys?.P256dh) || string.IsNullOrWhiteSpace(model.Keys?.Auth)
                || model.Keys.P256dh.Length > 200 || model.Keys.Auth.Length > 100)
            {
                return this.ToHttp(Core.Utilities.Results.OperationResult<bool>.BadRequest(ErrorCodes.Validation.InvalidFormat));
            }
            var ua = Request.Headers.UserAgent.ToString();
            PushSubscriptionStore.Save(User.GetPlayerId(), model.Endpoint, model.Keys.P256dh, model.Keys.Auth,
                ua.Length > 300 ? ua[..300] : ua);
            return Ok(new { Success = true });
        }

        /// <summary>Bu cihazın aboneliğini kaldırır.</summary>
        [HttpPost("Unsubscribe")]
        public IActionResult Unsubscribe(PushUnsubscribeModel model)
        {
            if (!string.IsNullOrWhiteSpace(model.Endpoint)) PushSubscriptionStore.Remove(User.GetPlayerId(), model.Endpoint);
            return Ok(new { Success = true });
        }
    }

    public class PushSubscribeModel
    {
        public string? Endpoint { get; set; }
        public PushKeysModel? Keys { get; set; }
    }

    public class PushKeysModel
    {
        public string? P256dh { get; set; }
        public string? Auth { get; set; }
    }

    public class PushUnsubscribeModel
    {
        public string? Endpoint { get; set; }
    }
}
