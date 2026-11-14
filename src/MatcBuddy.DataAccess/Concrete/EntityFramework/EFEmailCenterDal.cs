using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatcBuddy.DataAccess.Concrete.EntityFramework
{
    public class EFEmailCenterDal : IEmailCenterDal
    {
        public void EnsureDefaults(IEnumerable<EmailTemplate> defaults)
        {
            using var context = new MatcBuddyContext();
            var existing = context.EmailTemplates.Where(t => t.OwnerPlayerId == null).Select(t => t.Key).ToHashSet();
            foreach (var t in defaults.Where(d => !existing.Contains(d.Key))) context.EmailTemplates.Add(t);
            context.SaveChanges();
        }

        public List<EmailTemplate> Templates(int? ownerPlayerId)
        {
            using var context = new MatcBuddyContext();
            return context.EmailTemplates.AsNoTracking()
                .Where(t => t.OwnerPlayerId == ownerPlayerId)
                .OrderByDescending(t => t.IsSystem).ThenBy(t => t.Name)
                .ToList();
        }

        public EmailTemplate? Template(int id)
        {
            using var context = new MatcBuddyContext();
            return context.EmailTemplates.AsNoTracking().FirstOrDefault(t => t.EmailTemplateId == id);
        }

        public EmailTemplate? SystemTemplate(string key)
        {
            using var context = new MatcBuddyContext();
            return context.EmailTemplates.AsNoTracking().FirstOrDefault(t => t.OwnerPlayerId == null && t.Key == key);
        }

        public void SaveTemplate(EmailTemplate t)
        {
            using var context = new MatcBuddyContext();
            if (t.EmailTemplateId == 0) context.EmailTemplates.Add(t);
            else context.EmailTemplates.Update(t);
            context.SaveChanges();
        }

        public void DeleteTemplate(int id)
        {
            using var context = new MatcBuddyContext();
            context.EmailTemplates.Where(t => t.EmailTemplateId == id && !t.IsSystem).ExecuteDelete();
        }

        public List<EmailCampaign> Campaigns(int? ownerPlayerId)
        {
            using var context = new MatcBuddyContext();
            return context.EmailCampaigns.AsNoTracking()
                .Where(c => c.OwnerPlayerId == ownerPlayerId)
                .OrderByDescending(c => c.CreatedAt)
                .Take(200)
                .ToList();
        }

        public EmailCampaign? Campaign(int id)
        {
            using var context = new MatcBuddyContext();
            return context.EmailCampaigns.AsNoTracking().FirstOrDefault(c => c.EmailCampaignId == id);
        }

        public void SaveCampaign(EmailCampaign c)
        {
            using var context = new MatcBuddyContext();
            if (c.EmailCampaignId == 0) context.EmailCampaigns.Add(c);
            else context.EmailCampaigns.Update(c);
            context.SaveChanges();
        }

        public List<EmailCampaign> DueCampaigns(DateTime now)
        {
            using var context = new MatcBuddyContext();
            return context.EmailCampaigns.AsNoTracking()
                .Where(c => c.Status == EmailCampaignStatus.Sending
                            || (c.Status == EmailCampaignStatus.Scheduled && c.ScheduledAt <= now))
                .OrderBy(c => c.ScheduledAt)
                .ToList();
        }

        private static IQueryable<Player> Audience(MatcBuddyContext context, byte audience, int? ownerPlayerId)
        {
            var players = context.Players.AsNoTracking()
                .Where(p => !p.IsDeleted && p.Email != null && p.Email != "");
            switch (audience)
            {
                case EmailAudience.ConsentedPlayers:
                    return players.Where(p => p.Role == PlayerRoles.Player && p.MarketingConsent);
                case EmailAudience.Owners:
                    return players.Where(p => p.Role == PlayerRoles.Owner);
                case EmailAudience.AllPlayersService:
                    return players.Where(p => p.Role == PlayerRoles.Player);
                case EmailAudience.VenueCustomers:
                    if (ownerPlayerId == null) return players.Where(p => false);
                    var since = DateTime.Now.AddDays(-365);
                    var stadiumIds = context.Stadiums.Where(s => s.OwnerPlayerId == ownerPlayerId).Select(s => s.StadiumId);
                    var customerIds = from mp in context.MatchParticipants
                                      join m in context.Matchs on mp.MatchId equals m.MatchId
                                      where mp.Status == ParticipantStatus.Going
                                            && m.MatchDate >= since
                                            && stadiumIds.Contains(m.StadiumId)
                                      select mp.PlayerId;
                    return players.Where(p => p.Role == PlayerRoles.Player && p.MarketingConsent && customerIds.Contains(p.PlayerId));
                default:
                    return players.Where(p => false);
            }
        }

        public List<EmailRecipient> Recipients(byte audience, int? ownerPlayerId, int afterPlayerId, int take)
        {
            using var context = new MatcBuddyContext();
            return Audience(context, audience, ownerPlayerId)
                .Where(p => p.PlayerId > afterPlayerId)
                .OrderBy(p => p.PlayerId)
                .Take(take)
                .Select(p => new EmailRecipient
                {
                    PlayerId = p.PlayerId,
                    Email = p.Email!,
                    Name = p.PlayerName,
                    Surname = p.PlayerSurname,
                    UserName = p.UserName,
                })
                .ToList();
        }

        public int AudienceCount(byte audience, int? ownerPlayerId)
        {
            using var context = new MatcBuddyContext();
            return Audience(context, audience, ownerPlayerId).Count();
        }

        public DateTime? LastOwnerSend(int ownerPlayerId)
        {
            using var context = new MatcBuddyContext();
            return context.EmailCampaigns.AsNoTracking()
                .Where(c => c.OwnerPlayerId == ownerPlayerId && c.LastSentAt != null)
                .Max(c => c.LastSentAt);
        }

        public List<int> ReminderMinutes(int matchId, int playerId)
        {
            using var context = new MatcBuddyContext();
            return context.MatchEmailReminders.AsNoTracking()
                .Where(r => r.MatchId == matchId && r.PlayerId == playerId)
                .Select(r => r.MinutesBefore)
                .OrderBy(m => m)
                .ToList();
        }

        public void SetReminders(int matchId, int playerId, IEnumerable<int> minutes)
        {
            using var context = new MatcBuddyContext();
            var wanted = minutes.Distinct().ToHashSet();
            var rows = context.MatchEmailReminders.Where(r => r.MatchId == matchId && r.PlayerId == playerId).ToList();
            context.MatchEmailReminders.RemoveRange(rows.Where(r => !wanted.Contains(r.MinutesBefore)));
            foreach (var m in wanted.Where(m => rows.All(r => r.MinutesBefore != m)))
            {
                context.MatchEmailReminders.Add(new MatchEmailReminder
                {
                    MatchId = matchId,
                    PlayerId = playerId,
                    MinutesBefore = m,
                    CreatedAt = DateTime.Now,
                });
            }
            context.SaveChanges();
        }

        public List<DueEmailReminder> DueReminders(DateTime now, int take)
        {
            using var context = new MatcBuddyContext();
            var q = from r in context.MatchEmailReminders.AsNoTracking()
                    where r.SentAt == null
                    join m in context.Matchs.AsNoTracking() on r.MatchId equals m.MatchId
                    where m.MatchDate != null && m.MatchDate > now && m.IsActive != ParticipationCore.RejectedStatus
                    join p in context.Players.AsNoTracking() on r.PlayerId equals p.PlayerId
                    where !p.IsDeleted && p.Email != null && p.Email != ""
                    join mp in context.MatchParticipants.AsNoTracking() on new { r.MatchId, r.PlayerId } equals new { mp.MatchId, mp.PlayerId }
                    where mp.Status == ParticipantStatus.Going
                    join s in context.Stadiums.AsNoTracking() on m.StadiumId equals s.StadiumId into ms
                    from s in ms.DefaultIfEmpty()
                    select new { r, m, p, s };
            return q.AsEnumerable()
                .Where(x => x.m.MatchDate!.Value.AddMinutes(-x.r.MinutesBefore) <= now)
                .Take(take)
                .Select(x => new DueEmailReminder
                {
                    ReminderId = x.r.MatchEmailReminderId,
                    MinutesBefore = x.r.MinutesBefore,
                    PlayerId = x.p.PlayerId,
                    Email = x.p.Email!,
                    Name = x.p.PlayerName,
                    Surname = x.p.PlayerSurname,
                    UserName = x.p.UserName,
                    MatchId = x.m.MatchId,
                    MatchName = x.m.MatchName,
                    MatchDate = x.m.MatchDate!.Value,
                    StadiumName = x.s != null ? x.s.StadiumName : null,
                    Address = x.s != null ? x.s.Address : null,
                })
                .ToList();
        }

        public void MarkReminderSent(int reminderId, DateTime at)
        {
            using var context = new MatcBuddyContext();
            context.MatchEmailReminders.Where(r => r.MatchEmailReminderId == reminderId)
                .ExecuteUpdate(s => s.SetProperty(r => r.SentAt, at));
        }

        public bool InMatch(int matchId, int playerId)
        {
            using var context = new MatcBuddyContext();
            return context.MatchParticipants.Any(p => p.MatchId == matchId && p.PlayerId == playerId
                && (p.Status == ParticipantStatus.Going || p.Status == ParticipantStatus.Waitlist));
        }

        public string? OwnerVenueName(int ownerPlayerId)
        {
            using var context = new MatcBuddyContext();
            return context.Stadiums.AsNoTracking()
                .Where(st => st.OwnerPlayerId == ownerPlayerId)
                .OrderBy(st => st.StadiumId)
                .Select(st => st.StadiumName)
                .FirstOrDefault();
        }

        public Player? Player(int playerId)
        {
            using var context = new MatcBuddyContext();
            return context.Players.AsNoTracking().FirstOrDefault(p => p.PlayerId == playerId);
        }

        public void SetMarketingConsent(int playerId, bool value)
        {
            using var context = new MatcBuddyContext();
            context.Players.Where(p => p.PlayerId == playerId)
                .ExecuteUpdate(s => s.SetProperty(p => p.MarketingConsent, value).SetProperty(p => p.MarketingConsentAt, DateTime.UtcNow));
        }
    }
}
