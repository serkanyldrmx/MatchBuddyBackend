using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.DTOs;

namespace MatcBuddy.Business.Abstract
{
    /// <summary>
    /// E-posta merkezi: şablonlar, toplu / düzenli gönderimler (yönetici ve işletme), hoş geldin e-postası,
    /// maç hatırlatma e-postaları ve abonelikten çıkma. ownerPlayerId: null = yönetici.
    /// </summary>
    public interface IEmailCenterService
    {
        List<EmailTemplateDto> Templates(int? ownerPlayerId);
        /// <summary>Hazır şablon galerisi (yönetici / işletme)</summary>
        List<EmailStarterDto> Starters(bool owner);
        OperationResult<int> SaveTemplate(int? ownerPlayerId, EmailTemplateInput input);
        OperationResult<bool> DeleteTemplate(int? ownerPlayerId, int id);

        List<EmailCampaignDto> Campaigns(int? ownerPlayerId);
        OperationResult<int> SaveCampaign(int actorPlayerId, int? ownerPlayerId, EmailCampaignInput input);
        OperationResult<bool> CancelCampaign(int? ownerPlayerId, int id);
        int AudienceCount(int? ownerPlayerId, byte audience);

        EmailPreviewDto Preview(int actorPlayerId, EmailPreviewInput input);
        OperationResult<bool> SendTest(int actorPlayerId, EmailPreviewInput input);

        /// <summary>Kayıttan sonra (e-posta varsa) hoş geldin e-postası</summary>
        void SendWelcome(int playerId);

        MatchEmailRemindersDto GetReminders(int matchId, int playerId);
        OperationResult<bool> SetReminders(int playerId, MatchEmailRemindersInput input);

        /// <summary>Zamanlayıcı: varsayılan şablonlar, vadesi gelen kampanyalar ve hatırlatmalar. Gönderilen e-posta sayısı.</summary>
        int RunDue();

        OperationResult<bool> Unsubscribe(int playerId);
    }
}
