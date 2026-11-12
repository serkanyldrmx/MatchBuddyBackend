using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.DataAccess.Abstract
{
    /// <summary>Takip, engel, şikayet, ayarlar, mesaj izni, akış ve hesap silme (docs/social-api.md).</summary>
    public interface ISocialDal
    {
        // Data = isMutual
        OperationResult<bool> Follow(int me, int targetPlayerId);
        OperationResult<bool> Unfollow(int me, int targetPlayerId);
        OperationResult<List<SocialPlayerDto>> GetFollowers(int me, int playerId);
        OperationResult<List<SocialPlayerDto>> GetFollowing(int me, int playerId);
        List<SocialPlayerDto> Discover(int me, string? query, int take);
        List<SocialPlayerDto> Suggestions(int me, int take);

        // Stats alanı burada doldurulmaz (iş katmanı GetPlayerStats ile doldurur)
        OperationResult<PlayerProfileDto> GetProfile(int me, int playerId);

        OperationResult<bool> Block(int me, int targetPlayerId);
        OperationResult<bool> Unblock(int me, int targetPlayerId);
        List<PlayerSummaryDto> GetBlocked(int me);
        OperationResult<bool> Report(int me, int targetPlayerId, string reason, string? details);

        // Kayıt yoksa varsayılanlar (oluşturmaz)
        PlayerSettings GetSettings(int playerId);
        void SaveSettings(PlayerSettings settings);

        // Mesaj izni: Ok = gönderebilir; Forbidden/NotFound = gönderemez (mesajıyla)
        OperationResult<bool> CheckCanMessage(int senderId, int recipientId);

        List<MessageContactDto> GetMessageContacts(int me);
        List<ActivityDto> GetFeed(int me, int take, DateTime? before);

        // Takip/engel/ayar/davetleri siler, gelecek maçlardan Leave mantığıyla çıkarır, hesabı anonimleştirir
        bool DeleteAccount(int playerId, string randomPasswordHash);

        // Oyuncu listesi: silinmişler ve engeller (iki yön) hariç
        List<Player> GetVisiblePlayers(int viewerPlayerId);

        // Verilen id'lerden işletme (Role 2) olanlar
        HashSet<int> OwnerIds(IEnumerable<int> playerIds);

        // Admin
        List<AdminReportDto> GetReports(byte? status);
        OperationResult<bool> ResolveReport(int reportId);
    }
}
