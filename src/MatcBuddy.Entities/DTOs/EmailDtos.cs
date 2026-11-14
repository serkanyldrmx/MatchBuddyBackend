using MatcBuddy.Core.Entities;

namespace MatcBuddy.Entities.DTOs
{
    public class EmailTemplateDto : IDto
    {
        public int EmailTemplateId { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? ButtonText { get; set; }
        public string? ButtonUrl { get; set; }
        public bool IsSystem { get; set; }
        public bool IsActive { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>Hazır şablon (galeri; veritabanında tutulmaz)</summary>
    public class EmailStarterDto : IDto
    {
        public string Key { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? ButtonText { get; set; }
        public string? ButtonUrl { get; set; }
    }

    public class EmailTemplateInput : IDto
    {
        public int? EmailTemplateId { get; set; }
        public string? Name { get; set; }
        public string? Subject { get; set; }
        public string? Body { get; set; }
        public string? ButtonText { get; set; }
        public string? ButtonUrl { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class EmailCampaignDto : IDto
    {
        public int EmailCampaignId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? ButtonText { get; set; }
        public string? ButtonUrl { get; set; }
        public byte Audience { get; set; }
        public DateTime ScheduledAt { get; set; }
        public int? RepeatDays { get; set; }
        public byte Status { get; set; }
        public int SentCount { get; set; }
        public DateTime? LastSentAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class EmailCampaignInput : IDto
    {
        public int? EmailCampaignId { get; set; }
        public string? Subject { get; set; }
        public string? Body { get; set; }
        public string? ButtonText { get; set; }
        public string? ButtonUrl { get; set; }
        public int Audience { get; set; }
        /// <summary>Boş: hemen gönder</summary>
        public DateTime? ScheduledAt { get; set; }
        public int? RepeatDays { get; set; }
    }

    /// <summary>Şablon / kampanya önizleme ya da "kendime test gönder" gövdesi</summary>
    public class EmailPreviewInput : IDto
    {
        public string? Subject { get; set; }
        public string? Body { get; set; }
        public string? ButtonText { get; set; }
        public string? ButtonUrl { get; set; }
    }

    public class EmailPreviewDto : IDto
    {
        public string Subject { get; set; } = string.Empty;
        public string Html { get; set; } = string.Empty;
    }

    /// <summary>Alıcı (kampanya gönderimi)</summary>
    public class EmailRecipient
    {
        public int PlayerId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Surname { get; set; }
        public string? UserName { get; set; }
    }

    /// <summary>Gönderim zamanı gelmiş maç hatırlatması</summary>
    public class DueEmailReminder
    {
        public int ReminderId { get; set; }
        public int MinutesBefore { get; set; }
        public int PlayerId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Surname { get; set; }
        public string? UserName { get; set; }
        public int MatchId { get; set; }
        public string? MatchName { get; set; }
        public DateTime MatchDate { get; set; }
        public string? StadiumName { get; set; }
        public string? Address { get; set; }
    }

    /// <summary>Maç detayındaki "E-posta hatırlatıcı" seçimi</summary>
    public class MatchEmailRemindersDto
    {
        public List<int> Minutes { get; set; } = new();
        public List<int> Options { get; set; } = new();
        public bool HasEmail { get; set; }
    }

    public class MatchEmailRemindersInput
    {
        public int MatchId { get; set; }
        public List<int>? Minutes { get; set; }
    }
}
