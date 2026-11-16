using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.DTOs;

namespace MatcBuddy.Business.Abstract
{
    /// <summary>Admin → Kullanıcılar (arama, engelle / engeli kaldır) ve şikayet sonuçlandırma.</summary>
    public interface IModerationService
    {
        PagedDto<AdminUserDto> SearchUsers(string? query, bool bannedOnly, int page, int pageSize);

        /// <summary>Kullanıcıyı engeller. Oturumların kapatılması çağıranın işidir (IAuthService.RevokeAllSessions).</summary>
        OperationResult<bool> Ban(int adminId, BanUserInput input);
        OperationResult<bool> Unban(int playerId);

        /// <summary>Şikayeti sonuçlandırır. Data: engellenen oyuncunun Id'si (Action = engel ise), yoksa null.</summary>
        OperationResult<int?> ResolveReport(int adminId, ResolveReportInput input);
    }

    /// <summary>Herkese açık iletişim formu ve yönetim paneli "Mesajlar / İletişim".</summary>
    public interface IContactService
    {
        OperationResult<bool> Submit(ContactMessageInput input, int? playerId);
        PagedDto<ContactMessageDto> List(bool? handled, int page, int pageSize);
        int UnhandledCount();
        OperationResult<bool> SetHandled(ContactHandledInput input);
    }
}
