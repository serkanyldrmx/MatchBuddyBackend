namespace MatcBuddy.Entities.DTOs
{
    /// <summary>Admin → Kullanıcılar listesindeki satır. Şifre ve telefon dönmez.</summary>
    public class AdminUserDto
    {
        public int PlayerId { get; set; }
        public string? PlayerName { get; set; }
        public string? PlayerSurname { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? ProfilePictureUrl { get; set; }
        /// <summary>0 oyuncu, 1 admin, 2 işletme</summary>
        public byte Role { get; set; }
        public bool EmailConfirmed { get; set; }
        /// <summary>Engel şu an geçerli mi (süresi geçmiş engel false)</summary>
        public bool IsBanned { get; set; }
        public DateTime? BannedUntil { get; set; }
        public string? BanReason { get; set; }
        /// <summary>Bu kullanıcı hakkındaki tüm şikayetler</summary>
        public int ReportCount { get; set; }
        /// <summary>Henüz incelenmemiş şikayetler</summary>
        public int OpenReportCount { get; set; }
    }

    public class PagedDto<T>
    {
        public List<T> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class BanUserInput
    {
        public int PlayerId { get; set; }
        /// <summary>Bitiş (UTC). Boş = süresiz.</summary>
        public DateTime? Until { get; set; }
        public string? Reason { get; set; }
    }

    public class UnbanUserInput
    {
        public int PlayerId { get; set; }
    }

    /// <summary>
    /// Şikayeti sonuçlandır. Eski istemciler yalnızca { reportId } gönderir (Action = 0).
    /// Action = 2 (engel) ise şikayet edilen kullanıcı BanUntil / BanReason ile engellenir.
    /// </summary>
    public class ResolveReportInput
    {
        public int ReportId { get; set; }
        public string? Note { get; set; }
        public byte Action { get; set; }
        public DateTime? BanUntil { get; set; }
        public string? BanReason { get; set; }
    }

    public class ContactMessageInput
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Subject { get; set; }
        public string? Message { get; set; }
    }

    public class ContactMessageDto
    {
        public int ContactMessageId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Subject { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int? PlayerId { get; set; }
        public string? PlayerUserName { get; set; }
        public bool IsHandled { get; set; }
        public DateTime? HandledAt { get; set; }
    }

    public class ContactHandledInput
    {
        public int ContactMessageId { get; set; }
        /// <summary>false: yeniden "okunmadı" yap</summary>
        public bool Handled { get; set; } = true;
    }
}
