using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.DataAccess.Abstract
{
    public interface IEmailCenterDal
    {
        /// <summary>Eksik sistem şablonlarını ekler (var olanlara dokunmaz)</summary>
        void EnsureDefaults(IEnumerable<EmailTemplate> defaults);
        List<EmailTemplate> Templates(int? ownerPlayerId);
        EmailTemplate? Template(int id);
        EmailTemplate? SystemTemplate(string key);
        void SaveTemplate(EmailTemplate t);
        void DeleteTemplate(int id);

        List<EmailCampaign> Campaigns(int? ownerPlayerId);
        EmailCampaign? Campaign(int id);
        void SaveCampaign(EmailCampaign c);
        /// <summary>Zamanı gelmiş (ya da gönderimi süren) kampanyalar</summary>
        List<EmailCampaign> DueCampaigns(DateTime now);
        /// <summary>Kitle: PlayerId sırasıyla afterPlayerId'den sonraki en fazla take alıcı</summary>
        List<EmailRecipient> Recipients(byte audience, int? ownerPlayerId, int afterPlayerId, int take);
        int AudienceCount(byte audience, int? ownerPlayerId);
        /// <summary>İşletmenin son kampanya gönderimi (sıklık sınırı)</summary>
        DateTime? LastOwnerSend(int ownerPlayerId);

        List<int> ReminderMinutes(int matchId, int playerId);
        void SetReminders(int matchId, int playerId, IEnumerable<int> minutes);
        List<DueEmailReminder> DueReminders(DateTime now, int take);
        void MarkReminderSent(int reminderId, DateTime at);
        /// <summary>Oyuncu bu maçta (katılıyor / yedek) mi</summary>
        bool InMatch(int matchId, int playerId);

        Player? Player(int playerId);
        /// <summary>İşletmenin tesis adı (birden çok tesis varsa ilki); yoksa null</summary>
        string? OwnerVenueName(int ownerPlayerId);
        void SetMarketingConsent(int playerId, bool value);
    }
}
