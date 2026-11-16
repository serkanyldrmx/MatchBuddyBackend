using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatcBuddy.DataAccess.Concrete.EntityFramework
{
    public class EFModerationDal : IModerationDal
    {
        /// <summary>Veritabanından Kind = Unspecified gelen UTC tarih; JSON'da "Z" ile yazılsın diye işaretlenir.</summary>
        internal static DateTime? AsUtc(DateTime? value) =>
            value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;

        public PagedDto<AdminUserDto> SearchUsers(string? query, bool bannedOnly, int page, int pageSize, DateTime utcNow)
        {
            using var context = new MatcBuddyContext();
            var q = context.Players.AsNoTracking().Where(p => !p.IsDeleted);
            if (!string.IsNullOrWhiteSpace(query))
            {
                var term = query.Trim();
                // "Ad Soyad" biçiminde arama da eşleşsin
                q = q.Where(p => p.UserName!.Contains(term) || p.Email!.Contains(term)
                    || p.PlayerName!.Contains(term) || p.PlayerSurname!.Contains(term)
                    || (p.PlayerName + " " + p.PlayerSurname).Contains(term));
            }
            if (bannedOnly) q = q.Where(p => p.IsBanned && (p.BannedUntil == null || p.BannedUntil > utcNow));

            var total = q.Count();
            var rows = q.OrderBy(p => p.PlayerName).ThenBy(p => p.PlayerSurname).ThenBy(p => p.PlayerId)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToList();

            var ids = rows.Select(p => p.PlayerId).ToList();
            var counts = context.Reports.AsNoTracking()
                .Where(r => ids.Contains(r.ReportedPlayerId))
                .GroupBy(r => r.ReportedPlayerId)
                .Select(g => new { PlayerId = g.Key, All = g.Count(), Open = g.Count(r => r.Status == ReportStatus.New) })
                .ToList()
                .ToDictionary(x => x.PlayerId);

            return new PagedDto<AdminUserDto>
            {
                Total = total,
                Page = page,
                PageSize = pageSize,
                Items = rows.Select(p =>
                {
                    counts.TryGetValue(p.PlayerId, out var c);
                    var active = p.IsBanActive(utcNow);
                    return new AdminUserDto
                    {
                        PlayerId = p.PlayerId,
                        PlayerName = p.PlayerName,
                        PlayerSurname = p.PlayerSurname,
                        UserName = p.UserName,
                        Email = p.Email,
                        ProfilePictureUrl = p.ProfilePictureUrl,
                        Role = p.Role,
                        EmailConfirmed = p.EmailConfirmed,
                        IsBanned = active,
                        BannedUntil = active ? AsUtc(p.BannedUntil) : null,
                        BanReason = active ? p.BanReason : null,
                        ReportCount = c?.All ?? 0,
                        OpenReportCount = c?.Open ?? 0,
                    };
                }).ToList(),
            };
        }

        public Player? FindPlayer(int playerId)
        {
            using var context = new MatcBuddyContext();
            return context.Players.AsNoTracking().FirstOrDefault(p => p.PlayerId == playerId && !p.IsDeleted);
        }

        public void SetBan(int playerId, bool banned, DateTime? untilUtc, string? reason)
        {
            using var context = new MatcBuddyContext();
            var player = context.Players.FirstOrDefault(p => p.PlayerId == playerId);
            if (player == null) return;
            player.IsBanned = banned;
            player.BannedUntil = banned ? untilUtc : null;
            player.BanReason = banned ? reason : null;
            context.SaveChanges();
        }

        public Report? FindReport(int reportId)
        {
            using var context = new MatcBuddyContext();
            return context.Reports.AsNoTracking().FirstOrDefault(r => r.ReportId == reportId);
        }

        public void ResolveReport(int reportId, int adminId, string? note, byte action, DateTime utcNow)
        {
            using var context = new MatcBuddyContext();
            var report = context.Reports.FirstOrDefault(r => r.ReportId == reportId);
            if (report == null) return;
            report.Status = ReportStatus.Reviewed;
            report.ResolvedByPlayerId = adminId;
            report.ResolvedAt = utcNow;
            report.ResolutionNote = note;
            report.ResolutionAction = action;
            context.SaveChanges();
        }
    }

    public class EFContactDal : IContactDal
    {
        public void Add(ContactMessage message)
        {
            using var context = new MatcBuddyContext();
            context.ContactMessages.Add(message);
            context.SaveChanges();
        }

        public PagedDto<ContactMessageDto> List(bool? handled, int page, int pageSize)
        {
            using var context = new MatcBuddyContext();
            var q = context.ContactMessages.AsNoTracking();
            if (handled.HasValue) q = q.Where(m => m.IsHandled == handled.Value);
            var total = q.Count();
            var rows = q.OrderBy(m => m.IsHandled).ThenByDescending(m => m.CreatedAt).ThenByDescending(m => m.ContactMessageId)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToList();

            var playerIds = rows.Where(m => m.PlayerId.HasValue).Select(m => m.PlayerId!.Value).Distinct().ToList();
            var userNames = context.Players.AsNoTracking()
                .Where(p => playerIds.Contains(p.PlayerId) && !p.IsDeleted)
                .Select(p => new { p.PlayerId, p.UserName })
                .ToList()
                .ToDictionary(p => p.PlayerId, p => p.UserName);

            return new PagedDto<ContactMessageDto>
            {
                Total = total,
                Page = page,
                PageSize = pageSize,
                Items = rows.Select(m => new ContactMessageDto
                {
                    ContactMessageId = m.ContactMessageId,
                    Name = m.Name,
                    Email = m.Email,
                    Subject = m.Subject,
                    Message = m.Message,
                    CreatedAt = DateTime.SpecifyKind(m.CreatedAt, DateTimeKind.Utc),
                    PlayerId = m.PlayerId,
                    PlayerUserName = m.PlayerId.HasValue && userNames.TryGetValue(m.PlayerId.Value, out var u) ? u : null,
                    IsHandled = m.IsHandled,
                    HandledAt = EFModerationDal.AsUtc(m.HandledAt),
                }).ToList(),
            };
        }

        public int UnhandledCount()
        {
            using var context = new MatcBuddyContext();
            return context.ContactMessages.Count(m => !m.IsHandled);
        }

        public bool SetHandled(int contactMessageId, bool handled, DateTime utcNow)
        {
            using var context = new MatcBuddyContext();
            var m = context.ContactMessages.FirstOrDefault(x => x.ContactMessageId == contactMessageId);
            if (m == null) return false;
            m.IsHandled = handled;
            m.HandledAt = handled ? utcNow : null;
            context.SaveChanges();
            return true;
        }

        public List<(string Email, string Name)> AdminEmails()
        {
            using var context = new MatcBuddyContext();
            return context.Players.AsNoTracking()
                .Where(p => p.Role == PlayerRoles.Admin && !p.IsDeleted && p.Email != null && p.Email != "")
                .Select(p => new { p.Email, Name = (p.PlayerName + " " + p.PlayerSurname).Trim() })
                .AsEnumerable()
                .Select(p => (p.Email!, p.Name))
                .ToList();
        }
    }
}
