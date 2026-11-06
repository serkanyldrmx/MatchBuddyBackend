using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Api.Model
{
    /// <summary>
    /// Herkese gösterilebilen oyuncu bilgileri. İletişim bilgisi ve şifre İÇERMEZ.
    /// Alanlar PlayerSummaryDto'dan gelir (maç uç noktaları da aynı JSON şeklini kullanır).
    /// </summary>
    public class PublicPlayerDto : PlayerSummaryDto
    {
        public static PublicPlayerDto From(Player p) => new()
        {
            PlayerId = p.PlayerId,
            PlayerName = p.PlayerName,
            PlayerSurname = p.PlayerSurname,
            UserName = p.UserName,
            Size = p.Size,
            Weight = p.Weight,
            Age = p.Age,
            IsAdmin = p.IsAdmin,
            Role = p.Role,
            UserScore = p.UserScore,
            ProfilePictureUrl = p.ProfilePictureUrl,
        };
    }

    /// <summary>
    /// Oyuncunun kendisine (veya admin'e) gösterilen tam profil. Şifre İÇERMEZ.
    /// </summary>
    public class PlayerDto : PublicPlayerDto
    {
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }

        public static new PlayerDto From(Player p) => new()
        {
            PlayerId = p.PlayerId,
            PlayerName = p.PlayerName,
            PlayerSurname = p.PlayerSurname,
            UserName = p.UserName,
            Size = p.Size,
            Weight = p.Weight,
            Age = p.Age,
            IsAdmin = p.IsAdmin,
            Role = p.Role,
            UserScore = p.UserScore,
            ProfilePictureUrl = p.ProfilePictureUrl,
            Email = p.Email,
            PhoneNumber = p.PhoneNumber,
            Address = p.Address,
        };
    }

    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public PlayerDto Player { get; set; } = null!;
        // 0 oyuncu, 1 admin, 2 işletme (player.role ile aynı)
        public byte Role { get; set; }
    }
}
