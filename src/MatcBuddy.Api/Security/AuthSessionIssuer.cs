using MatcBuddy.Api.Model;
using MatcBuddy.Business.Auth;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Api.Security
{
    /// <summary>Giriş yanıtı üretir: access token (JSON gövdesinde) + refresh token (HttpOnly çerez). docs/auth-api.md.</summary>
    public sealed class AuthSessionIssuer
    {
        private readonly IAuthService _auth;
        private readonly IJwtTokenService _jwt;
        private readonly RefreshCookie _cookie;

        public AuthSessionIssuer(IAuthService auth, IJwtTokenService jwt, RefreshCookie cookie)
        {
            _auth = auth;
            _jwt = jwt;
            _cookie = cookie;
        }

        public static ClientInfo Client(HttpContext ctx) =>
            new(ctx.Connection.RemoteIpAddress?.ToString(), ctx.Request.Headers.UserAgent.ToString());

        /// <summary>Yeni oturum (yeni refresh ailesi) açar, çerezi yazar.</summary>
        public AuthLoginResponse SignIn(HttpContext ctx, Player player, bool rememberMe, bool isNew = false, int? accessMinutes = null)
        {
            var session = _auth.StartSession(player.PlayerId, rememberMe, Client(ctx));
            _cookie.Write(ctx, session);
            return Build(player, session.ExpiresAtUtc, rememberMe, isNew, accessMinutes);
        }

        public AuthLoginResponse Build(Player player, DateTime? refreshExpiresAt, bool rememberMe, bool isNew = false, int? accessMinutes = null)
        {
            var (token, expiresAt) = _jwt.CreateToken(player, accessMinutes);
            var missing = _auth.MissingProfileFields(player);
            return new AuthLoginResponse
            {
                Token = token,
                ExpiresAt = expiresAt,
                Player = PlayerDto.From(player),
                Role = player.Role,
                RefreshExpiresAt = refreshExpiresAt,
                RememberMe = rememberMe,
                ConsentRequired = _auth.ConsentRequired(player),
                TermsVersion = _auth.CurrentTermsVersion,
                EmailConfirmed = player.EmailConfirmed,
                IsNew = isNew,
                ProfileIncomplete = missing.Count > 0,
                MissingProfileFields = missing,
            };
        }
    }
}
