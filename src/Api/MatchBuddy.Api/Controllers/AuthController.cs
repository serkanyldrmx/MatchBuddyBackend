using MatchBuddy.Api.Model;
using MatchBuddy.Api.Security;
using MatchBuddy.Api.Services.Auth;
using MatchBuddy.Business.Abstract;
using MatchBuddy.Business.Auth;
using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace MatchBuddy.Api.Controllers
{
    /// <summary>Oturum, sosyal giriş, şifre sıfırlama, e-posta doğrulama, KVKK onayı — docs/auth-api.md.</summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AuthController : ControllerBase
    {
        public const string PasswordResetPolicy = "password-reset";

        private readonly IAuthService _auth;
        private readonly IPlayerService _players;
        private readonly AuthSessionIssuer _issuer;
        private readonly RefreshCookie _cookie;
        private readonly ExternalSignInService _external;
        private readonly ExternalAvatarImporter _avatars;
        private readonly IdentifierThrottle _throttle;
        private readonly ExternalAuthOptions _externalOptions;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService auth, IPlayerService players, AuthSessionIssuer issuer, RefreshCookie cookie,
            ExternalSignInService external, ExternalAvatarImporter avatars, IdentifierThrottle throttle,
            IOptions<ExternalAuthOptions> externalOptions, ILogger<AuthController> logger)
        {
            _auth = auth;
            _players = players;
            _issuer = issuer;
            _cookie = cookie;
            _external = external;
            _avatars = avatars;
            _throttle = throttle;
            _externalOptions = externalOptions.Value;
            _logger = logger;
        }

        // ================================================================== Oturum

        /// <summary>Kullanıcı adı + şifre. Gövde: { token, expiresAt, player, role, … }; refresh token mb_rt çerezinde.</summary>
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [HttpPost("Login")]
        public ActionResult<AuthLoginResponse> Login(AuthLoginModel model)
        {
            var result = _players.LoginToGetPlayer(new PlayerLogin { UserName = model.UserName, Password = model.Password });
            if (!result.Success || result.Data == null)
            {
                _logger.LogWarning("Başarısız giriş denemesi: {UserName}", model.UserName);
                return this.UnauthorizedError(ErrorCodes.Auth.InvalidCredentials);
            }
            return _issuer.SignIn(HttpContext, result.Data, model.RememberMe);
        }

        /// <summary>Çerezdeki refresh token → yeni access token + döndürülmüş refresh çerezi.</summary>
        [AllowAnonymous]
        [HttpPost("Refresh")]
        public ActionResult<AuthLoginResponse> Refresh()
        {
            var result = _auth.Refresh(_cookie.Read(HttpContext), AuthSessionIssuer.Client(HttpContext));
            if (!result.Success || result.Data == null)
            {
                _cookie.Clear(HttpContext);
                return this.ToHttp(result);
            }
            var (player, session) = (result.Data.Player, result.Data.NewSession);
            if (session != null) _cookie.Write(HttpContext, session);
            return _issuer.Build(player, session?.ExpiresAtUtc, session?.Persistent ?? true);
        }

        /// <summary>Bu cihazdaki oturumu kapatır (çerez yeterli; access token gerekmez).</summary>
        [AllowAnonymous]
        [HttpPost("Logout")]
        public IActionResult Logout()
        {
            _auth.Logout(_cookie.Read(HttpContext));
            _cookie.Clear(HttpContext);
            return Ok(new { success = true, message = Msg.Get(MessageKeys.Auth.LoggedOut) });
        }

        /// <summary>Tüm cihazlardaki oturumları kapatır.</summary>
        [HttpPost("LogoutAll")]
        public IActionResult LogoutAll()
        {
            var count = _auth.LogoutAll(User.GetPlayerId());
            _cookie.Clear(HttpContext);
            return Ok(new { success = true, message = Msg.Get(MessageKeys.Auth.LoggedOutAll), revokedSessions = count });
        }

        /// <summary>Şifre değiştir: diğer tüm oturumlar kapanır, bu cihaza yeni oturum verilir.</summary>
        [EnableRateLimiting("auth")]
        [HttpPost("ChangePassword")]
        public ActionResult<AuthLoginResponse> ChangePassword(ChangePasswordModel model)
        {
            var remember = _auth.IsPersistent(_cookie.Read(HttpContext)) ?? true;
            var result = _auth.ChangePassword(User.GetPlayerId(), model.CurrentPassword, model.NewPassword);
            if (!result.Success || result.Data == null) return this.ToHttp(result);
            return _issuer.SignIn(HttpContext, result.Data, remember);
        }

        // ================================================================== Sosyal giriş

        /// <summary>Yapılandırılmış sağlayıcılar (frontend yalnızca bunların butonlarını gösterir).</summary>
        [AllowAnonymous]
        [HttpGet("Providers")]
        public IActionResult Providers() => Ok(new
        {
            google = _externalOptions.Google.IsConfigured ? new { clientId = _externalOptions.Google.ClientId } : null,
            facebook = _externalOptions.Facebook.IsConfigured ? new { appId = _externalOptions.Facebook.AppId } : null,
        });

        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [HttpPost("Google")]
        public Task<ActionResult<AuthLoginResponse>> Google(GoogleLoginModel model, CancellationToken ct) =>
            ExternalSignIn(ExternalProviders.Google, model.IdToken, model.RememberMe, ct);

        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [HttpPost("Facebook")]
        public Task<ActionResult<AuthLoginResponse>> Facebook(FacebookLoginModel model, CancellationToken ct) =>
            ExternalSignIn(ExternalProviders.Facebook, model.AccessToken, model.RememberMe, ct);

        private async Task<ActionResult<AuthLoginResponse>> ExternalSignIn(string provider, string? token, bool rememberMe, CancellationToken ct)
        {
            var result = await _external.SignInAsync(provider, token, ct);
            if (!result.Success || result.Data == null) return AuthError(result);

            var player = result.Data.Player;
            if (result.Data.IsNew && !string.IsNullOrEmpty(result.Data.Identity.PictureUrl))
            {
                var url = await _avatars.ImportAsync(player.PlayerId, result.Data.Identity.PictureUrl, ct);
                if (url != null) player = _auth.GetPlayer(player.PlayerId) ?? player;
            }
            _logger.LogInformation("Sosyal giriş: {Provider}, oyuncu {PlayerId} (yeni: {IsNew}, bağlandı: {Linked})",
                provider, player.PlayerId, result.Data.IsNew, result.Data.Linked);
            return _issuer.SignIn(HttpContext, player, rememberMe, result.Data.IsNew);
        }

        [HttpGet("ExternalLogins")]
        public IActionResult ExternalLogins()
        {
            var me = _auth.GetPlayer(User.GetPlayerId());
            if (me == null) return this.NotFoundError(ErrorCodes.Player.NotFound);
            return Ok(new
            {
                hasPassword = me.HasPassword,
                logins = _auth.GetExternalLogins(me.PlayerId).Select(l => new
                {
                    provider = l.Provider, email = l.Email, displayName = l.DisplayName, createdAt = l.CreatedAt, lastUsedAt = l.LastUsedAt,
                }),
            });
        }

        [HttpDelete("ExternalLogins/{provider}")]
        public IActionResult RemoveExternalLogin(string provider) =>
            this.ToHttp(_auth.UnlinkExternal(User.GetPlayerId(), provider));

        // ================================================================== Şifre sıfırlama

        /// <summary>Her zaman aynı 200 yanıtı (hesap listelenemez). Kod e-posta ya da SMS ile hesabın kayıtlı adresine gider.</summary>
        [AllowAnonymous]
        [EnableRateLimiting(PasswordResetPolicy)]
        [HttpPost("ForgotPassword")]
        public IActionResult ForgotPassword(ForgotPasswordModel model)
        {
            var throttled = Throttle("forgot", model.Identifier);
            if (throttled != null) return throttled;
            return this.ToHttp(_auth.ForgotPassword(model.Identifier, model.Channel, AuthSessionIssuer.Client(HttpContext)));
        }

        [AllowAnonymous]
        [EnableRateLimiting(PasswordResetPolicy)]
        [HttpPost("ResetPassword")]
        public IActionResult ResetPassword(ResetPasswordModel model)
        {
            var throttled = Throttle("reset", model.Identifier);
            if (throttled != null) return throttled;
            var result = _auth.ResetPassword(model.Identifier, model.Code, model.NewPassword);
            if (result.Success) _cookie.Clear(HttpContext);
            return this.ToHttp(result, result.Success ? new { success = true, message = result.Message } : null);
        }

        private ActionResult? Throttle(string scope, string? identifier)
        {
            var wait = _throttle.Hit(scope, identifier);
            if (wait == null) return null;
            Response.Headers.RetryAfter = wait.Value.ToString();
            return this.Error(StatusCodes.Status429TooManyRequests, ErrorCodes.Common.RateLimited);
        }

        // ================================================================== E-posta doğrulama

        [HttpPost("VerifyEmail")]
        public IActionResult VerifyEmail(VerifyEmailModel model)
        {
            var result = _auth.VerifyEmail(User.GetPlayerId(), model.Code);
            return this.ToHttp(result, result.Success ? new { success = true, message = result.Message, player = PlayerDto.From(result.Data!) } : null);
        }

        [HttpPost("ResendVerification")]
        public IActionResult ResendVerification()
        {
            var result = _auth.SendEmailVerification(User.GetPlayerId(), enforceCooldown: true, AuthSessionIssuer.Client(HttpContext));
            if (!result.Success) return AuthError(result);
            return Ok(new { success = true, message = result.Message });
        }

        // ================================================================== KVKK

        [HttpPost("AcceptTerms")]
        public IActionResult AcceptTerms(AcceptTermsModel model)
        {
            var result = _auth.AcceptTerms(User.GetPlayerId(), model.TermsVersion, model.MarketingConsent);
            return this.ToHttp(result, result.Success
                ? new { success = true, message = result.Message, consentRequired = false, player = PlayerDto.From(result.Data!) }
                : null);
        }

        // ================================================================== Yardımcılar

        /// <summary>Kimlik hataları: AUTH_PROVIDER_UNAVAILABLE → 502, AUTH_CODE_RESEND_COOLDOWN → 429 + Retry-After.</summary>
        private ActionResult AuthError<T>(OperationResult<T> result)
        {
            if (result.Success) return Ok(result);
            if (result.Code == ErrorCodes.Auth.ProviderUnavailable)
                return this.Error(StatusCodes.Status502BadGateway, result.Code, result.Message);
            if (result.Code == ErrorCodes.Auth.CodeResendCooldown)
            {
                Response.Headers.RetryAfter = Convert.ToString(result.Args.FirstOrDefault() ?? 60, System.Globalization.CultureInfo.InvariantCulture);
                return this.Error(StatusCodes.Status429TooManyRequests, result.Code, result.Message);
            }
            return this.ToHttp(result);
        }
    }
}
