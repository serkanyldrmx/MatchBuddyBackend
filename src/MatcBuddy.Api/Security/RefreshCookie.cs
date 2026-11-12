using MatcBuddy.Business.Auth;

namespace MatcBuddy.Api.Security
{
    /// <summary>Refresh token çerezi ayarları (Auth:RefreshCookie). docs/auth-api.md §Çerez.</summary>
    public sealed class RefreshCookieOptions
    {
        public const string SectionName = "Auth:RefreshCookie";
        public string Name { get; set; } = "mb_rt";
        public string Path { get; set; } = "/api/Auth";
        /// <summary>Lax (varsayılan; frontend ve API aynı site) | Strict | None (farklı sitelerde; Secure zorunlu olur).</summary>
        public string SameSite { get; set; } = "Lax";
        /// <summary>Boş = yalnızca API host'u.</summary>
        public string? Domain { get; set; }
    }

    /// <summary>
    /// HttpOnly refresh çerezi. Secure: istek https ise (X-Forwarded-Proto güvenilir vekilden geldiyse ona göre) ya da SameSite=None.
    /// "Beni hatırla" kapalıysa Expires yazılmaz (tarayıcı kapanınca silinen oturum çerezi).
    /// </summary>
    public sealed class RefreshCookie
    {
        private readonly RefreshCookieOptions _o;
        private readonly SameSiteMode _sameSite;

        public RefreshCookie(Microsoft.Extensions.Options.IOptions<RefreshCookieOptions> options)
        {
            _o = options.Value;
            _sameSite = Enum.TryParse<SameSiteMode>(_o.SameSite, true, out var m) && m != SameSiteMode.Unspecified ? m : SameSiteMode.Lax;
        }

        public string Name => _o.Name;

        private CookieOptions Options(HttpContext ctx, DateTimeOffset? expires) => new()
        {
            HttpOnly = true,
            Secure = ctx.Request.IsHttps || _sameSite == SameSiteMode.None,
            SameSite = _sameSite,
            Path = _o.Path,
            Domain = string.IsNullOrWhiteSpace(_o.Domain) ? null : _o.Domain,
            Expires = expires,
            IsEssential = true,
        };

        public string? Read(HttpContext ctx) => ctx.Request.Cookies[_o.Name];

        public void Write(HttpContext ctx, IssuedSession session) =>
            ctx.Response.Cookies.Append(_o.Name, session.RefreshToken,
                Options(ctx, session.Persistent ? new DateTimeOffset(session.ExpiresAtUtc, TimeSpan.Zero) : null));

        public void Clear(HttpContext ctx) =>
            ctx.Response.Cookies.Delete(_o.Name, Options(ctx, null));
    }
}
