using MatcBuddy.Core.Entities;

namespace MatcBuddy.Entities.Entity
{
    /// <summary>
    /// E-posta şablonu. Gövde düz metindir; {{ad}}, {{mac}} gibi alanlar gönderimde doldurulur,
    /// markalı HTML yerleşimine yerleştirilir. Sistem şablonları (hoş geldin, maç hatırlatma) silinemez.
    /// OwnerPlayerId: işletmenin kendi şablonu (null = yönetici / sistem).
    /// </summary>
    public class EmailTemplate : IEntity
    {
        public int EmailTemplateId { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? ButtonText { get; set; }
        public string? ButtonUrl { get; set; }
        public bool IsSystem { get; set; }
        public bool IsActive { get; set; } = true;
        public int? OwnerPlayerId { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>Toplu / düzenli e-posta gönderimi (yönetici ya da işletme).</summary>
    public class EmailCampaign : IEntity
    {
        public int EmailCampaignId { get; set; }
        public int CreatedByPlayerId { get; set; }
        /// <summary>İşletme gönderimi: işletme sahibi (null = yönetici)</summary>
        public int? OwnerPlayerId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? ButtonText { get; set; }
        public string? ButtonUrl { get; set; }
        public byte Audience { get; set; }
        public DateTime ScheduledAt { get; set; }
        /// <summary>Tekrar: null tek sefer, 7 haftalık, 30 aylık</summary>
        public int? RepeatDays { get; set; }
        public byte Status { get; set; } = EmailCampaignStatus.Scheduled;
        /// <summary>Gönderim sürerken kaldığı oyuncu (sırayla, parça parça gönderilir)</summary>
        public int Cursor { get; set; }
        public int SentCount { get; set; }
        public DateTime? LastSentAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>Oyuncunun seçtiği maç hatırlatma e-postası (ör. 180 = 3 saat önce)</summary>
    public class MatchEmailReminder : IEntity
    {
        public int MatchEmailReminderId { get; set; }
        public int MatchId { get; set; }
        public int PlayerId { get; set; }
        public int MinutesBefore { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public static class EmailTemplateKeys
    {
        public const string Welcome = "welcome";
        public const string MatchReminder = "match_reminder";
    }

    public static class EmailAudience
    {
        /// <summary>Kampanya / duyuru e-postası almayı kabul eden oyuncular (pazarlama izni)</summary>
        public const byte ConsentedPlayers = 0;
        /// <summary>İşletme hesapları (hizmet bilgilendirmesi)</summary>
        public const byte Owners = 1;
        /// <summary>Tüm oyuncular — yalnızca hizmet bildirimi (bakım, kural değişikliği); reklam içermemeli</summary>
        public const byte AllPlayersService = 2;
        /// <summary>İşletme: tesiste son 1 yılda oynamış ve pazarlama izni veren oyuncular</summary>
        public const byte VenueCustomers = 3;
    }

    public static class EmailCampaignStatus
    {
        public const byte Scheduled = 0;
        public const byte Sending = 1;
        public const byte Done = 2;
        public const byte Cancelled = 3;
    }

    public static class EmailReminderOptions
    {
        /// <summary>Seçilebilen hatırlatma süreleri (dakika)</summary>
        public static readonly int[] Minutes = { 60, 180, 1440 };
    }
}
