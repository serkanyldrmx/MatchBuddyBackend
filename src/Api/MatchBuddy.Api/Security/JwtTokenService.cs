using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MatchBuddy.Entities.Entity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MatchBuddy.Api.Security
{
    public class JwtOptions
    {
        public const string SectionName = "Jwt";

        public string Issuer { get; set; } = "MatchBuddy.Api";
        public string Audience { get; set; } = "MatchBuddy.Client";

        /// <summary>
        /// İmzalama anahtarı. appsettings'e YAZILMAZ; user-secrets veya ortam değişkeni (Jwt__Key) ile verilir.
        /// En az 32 karakter olmalıdır.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        // Not: token süresi artık yapılandırmada değil, sistem parametresinde: auth.accessTokenMinutes (docs/auth-api.md).
    }

    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Player = "Player";
        public const string Owner = "Owner";

        /// <summary>Players.Role (0 oyuncu, 1 admin, 2 işletme) -> JWT rol claim'i. IsAdmin geriye uyumluluk için de dikkate alınır.</summary>
        public static string FromPlayer(Player player) =>
            player.Role == PlayerRoles.Admin || player.IsAdmin == 1 ? Admin
            : player.Role == PlayerRoles.Owner ? Owner
            : Player;
    }

    public interface IJwtTokenService
    {
        /// <summary>Access token. <paramref name="minutes"/> verilmezse auth.accessTokenMinutes.</summary>
        (string Token, DateTime ExpiresAt) CreateToken(Player player, int? minutes = null);
    }

    public class JwtTokenService : IJwtTokenService
    {
        private readonly JwtOptions _options;

        public JwtTokenService(IOptions<JwtOptions> options)
        {
            _options = options.Value;
        }

        public (string Token, DateTime ExpiresAt) CreateToken(Player player, int? minutes = null)
        {
            var expiresAt = DateTime.UtcNow.AddMinutes(minutes ?? P.Int(ParameterKeys.Auth.AccessTokenMinutes));

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, player.PlayerId.ToString()),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(ClaimTypes.NameIdentifier, player.PlayerId.ToString()),
                new(ClaimTypes.Name, player.UserName ?? string.Empty),
                new(ClaimTypes.Role, Roles.FromPlayer(player)),
            };

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
        }
    }

    public static class ClaimsPrincipalExtensions
    {
        /// <summary>Token'daki oyuncu id'si. [Authorize] olan endpoint'lerde her zaman doludur.</summary>
        public static int GetPlayerId(this ClaimsPrincipal user)
        {
            var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var id) ? id : 0;
        }

        public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin);

        public static bool IsOwner(this ClaimsPrincipal user) => user.IsInRole(Roles.Owner);

        /// <summary>İstek sahibi bu oyuncunun kendisi mi ya da admin mi?</summary>
        public static bool IsSelfOrAdmin(this ClaimsPrincipal user, int playerId) =>
            user.IsAdmin() || user.GetPlayerId() == playerId;
    }
}
