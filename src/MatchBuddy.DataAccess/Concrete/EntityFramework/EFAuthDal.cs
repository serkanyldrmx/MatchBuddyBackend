using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatchBuddy.DataAccess.Concrete.EntityFramework
{
    /// <summary>Oturum / kod / sosyal giriş / dışa aktarım veri erişimi (docs/auth-api.md).</summary>
    public class EFAuthDal : IAuthDal
    {
        // ------------------------------------------------------------------ Refresh token

        public void AddRefreshToken(RefreshToken token)
        {
            using var context = new MatchBuddyContext();
            context.RefreshTokens.Add(token);
            context.SaveChanges();
        }

        public RefreshToken? FindRefreshToken(string tokenHash)
        {
            using var context = new MatchBuddyContext();
            return context.RefreshTokens.AsNoTracking().FirstOrDefault(t => t.TokenHash == tokenHash);
        }

        public RefreshToken? GetRefreshToken(long id)
        {
            using var context = new MatchBuddyContext();
            return context.RefreshTokens.AsNoTracking().FirstOrDefault(t => t.RefreshTokenId == id);
        }

        public bool TryRotate(long oldId, RefreshToken replacement, DateTime nowUtc)
        {
            using var context = new MatchBuddyContext();
            using var tx = context.Database.BeginTransaction();
            context.RefreshTokens.Add(replacement);
            context.SaveChanges();
            // Koşullu güncelleme: aynı token iki istekte eş zamanlı döndürülemez
            var updated = context.RefreshTokens
                .Where(t => t.RefreshTokenId == oldId && t.RevokedAt == null)
                .ExecuteUpdate(s => s
                    .SetProperty(t => t.RevokedAt, nowUtc)
                    .SetProperty(t => t.RevokedReason, RevokeReasons.Rotated)
                    .SetProperty(t => t.ReplacedById, replacement.RefreshTokenId));
            if (updated == 0)
            {
                tx.Rollback();
                return false;
            }
            tx.Commit();
            return true;
        }

        public bool Revoke(long id, string reason, DateTime nowUtc)
        {
            using var context = new MatchBuddyContext();
            return context.RefreshTokens
                .Where(t => t.RefreshTokenId == id && t.RevokedAt == null)
                .ExecuteUpdate(s => s.SetProperty(t => t.RevokedAt, nowUtc).SetProperty(t => t.RevokedReason, reason)) > 0;
        }

        public int RevokeFamily(Guid familyId, string reason, DateTime nowUtc)
        {
            using var context = new MatchBuddyContext();
            return context.RefreshTokens
                .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
                .ExecuteUpdate(s => s.SetProperty(t => t.RevokedAt, nowUtc).SetProperty(t => t.RevokedReason, reason));
        }

        public int RevokeAllForPlayer(int playerId, string reason, DateTime nowUtc)
        {
            using var context = new MatchBuddyContext();
            return context.RefreshTokens
                .Where(t => t.PlayerId == playerId && t.RevokedAt == null)
                .ExecuteUpdate(s => s.SetProperty(t => t.RevokedAt, nowUtc).SetProperty(t => t.RevokedReason, reason));
        }

        // ------------------------------------------------------------------ Oyuncu

        public Player? FindPlayer(int playerId)
        {
            using var context = new MatchBuddyContext();
            return context.Players.AsNoTracking().FirstOrDefault(p => p.PlayerId == playerId);
        }

        public Player? FindByUserName(string userName)
        {
            using var context = new MatchBuddyContext();
            return context.Players.AsNoTracking().FirstOrDefault(p => p.UserName == userName && !p.IsDeleted);
        }

        public List<Player> FindPlayersByEmail(string email)
        {
            var normalized = email.Trim().ToLower();
            using var context = new MatchBuddyContext();
            return context.Players.AsNoTracking()
                .Where(p => !p.IsDeleted && p.Email != null && p.Email.Trim().ToLower() == normalized)
                .ToList();
        }

        public List<Player> FindPlayersByPhone(string last10Digits)
        {
            if (last10Digits.Length < 7) return new List<Player>();
            var tail = last10Digits[^4..];
            using var context = new MatchBuddyContext();
            // Telefon serbest biçimde saklanıyor: son 4 hane ile ön eleme, sonra bellekte rakamlara göre karşılaştırma
            return context.Players.AsNoTracking()
                .Where(p => !p.IsDeleted && p.PhoneNumber != null && p.PhoneNumber.Contains(tail))
                .AsEnumerable()
                .Where(p => PhoneDigits.Last10(p.PhoneNumber) == last10Digits)
                .ToList();
        }

        public bool UserNameExists(string userName)
        {
            using var context = new MatchBuddyContext();
            return context.Players.Any(p => p.UserName == userName);
        }

        public void SetPassword(int playerId, string passwordHash)
        {
            using var context = new MatchBuddyContext();
            context.Players.Where(p => p.PlayerId == playerId)
                .ExecuteUpdate(s => s.SetProperty(p => p.Password, passwordHash).SetProperty(p => p.HasPassword, true));
        }

        public bool SetEmailConfirmed(int playerId, string email)
        {
            var normalized = email.Trim().ToLower();
            using var context = new MatchBuddyContext();
            return context.Players
                .Where(p => p.PlayerId == playerId && p.Email != null && p.Email.Trim().ToLower() == normalized)
                .ExecuteUpdate(s => s.SetProperty(p => p.EmailConfirmed, true)) > 0;
        }

        public bool SetPhoneConfirmed(int playerId, string phone)
        {
            using var context = new MatchBuddyContext();
            return context.Players
                .Where(p => p.PlayerId == playerId && p.PhoneNumber == phone)
                .ExecuteUpdate(s => s.SetProperty(p => p.PhoneConfirmed, true)) > 0;
        }

        public void SetConsent(int playerId, string termsVersion, bool marketingConsent, DateTime nowUtc)
        {
            using var context = new MatchBuddyContext();
            var player = context.Players.FirstOrDefault(p => p.PlayerId == playerId);
            if (player == null) return;
            player.TermsVersion = termsVersion;
            player.TermsAcceptedAt = nowUtc;
            player.PrivacyAcceptedAt = nowUtc;
            if (player.MarketingConsent != marketingConsent || player.MarketingConsentAt == null)
            {
                player.MarketingConsent = marketingConsent;
                player.MarketingConsentAt = nowUtc;
            }
            context.SaveChanges();
        }

        public void SetAvatar(int playerId, string url, int? mediaId)
        {
            using var context = new MatchBuddyContext();
            context.Players.Where(p => p.PlayerId == playerId)
                .ExecuteUpdate(s => s.SetProperty(p => p.ProfilePictureUrl, url).SetProperty(p => p.ProfileMediaId, mediaId));
        }

        // ------------------------------------------------------------------ Kodlar

        public AuthCode? LatestCode(int playerId, byte purpose)
        {
            using var context = new MatchBuddyContext();
            return context.AuthCodes.AsNoTracking()
                .Where(c => c.PlayerId == playerId && c.Purpose == purpose)
                .OrderByDescending(c => c.AuthCodeId)
                .FirstOrDefault();
        }

        public void ReplaceCode(AuthCode code)
        {
            using var context = new MatchBuddyContext();
            using var tx = context.Database.BeginTransaction();
            context.AuthCodes
                .Where(c => c.PlayerId == code.PlayerId && c.Purpose == code.Purpose && c.ConsumedAt == null)
                .ExecuteUpdate(s => s.SetProperty(c => c.ConsumedAt, code.CreatedAt));
            context.AuthCodes.Add(code);
            context.SaveChanges();
            tx.Commit();
        }

        public bool TryUseAttempt(long authCodeId, int maxAttempts)
        {
            using var context = new MatchBuddyContext();
            return context.AuthCodes
                .Where(c => c.AuthCodeId == authCodeId && c.ConsumedAt == null && c.Attempts < maxAttempts)
                .ExecuteUpdate(s => s.SetProperty(c => c.Attempts, c => c.Attempts + 1)) > 0;
        }

        public bool ConsumeCode(long authCodeId, DateTime nowUtc)
        {
            using var context = new MatchBuddyContext();
            return context.AuthCodes
                .Where(c => c.AuthCodeId == authCodeId && c.ConsumedAt == null)
                .ExecuteUpdate(s => s.SetProperty(c => c.ConsumedAt, nowUtc)) > 0;
        }

        public void InvalidateCodes(int playerId, byte purpose, DateTime nowUtc)
        {
            using var context = new MatchBuddyContext();
            context.AuthCodes
                .Where(c => c.PlayerId == playerId && c.Purpose == purpose && c.ConsumedAt == null)
                .ExecuteUpdate(s => s.SetProperty(c => c.ConsumedAt, nowUtc));
        }

        // ------------------------------------------------------------------ Sosyal giriş

        public ExternalLogin? FindLink(string provider, string providerKey)
        {
            using var context = new MatchBuddyContext();
            return context.ExternalLogins.AsNoTracking()
                .FirstOrDefault(l => l.Provider == provider && l.ProviderKey == providerKey);
        }

        public void TouchLink(int externalLoginId, DateTime nowUtc)
        {
            using var context = new MatchBuddyContext();
            context.ExternalLogins.Where(l => l.ExternalLoginId == externalLoginId)
                .ExecuteUpdate(s => s.SetProperty(l => l.LastUsedAt, nowUtc));
        }

        public void RemoveLinkById(int externalLoginId)
        {
            using var context = new MatchBuddyContext();
            context.ExternalLogins.Where(l => l.ExternalLoginId == externalLoginId).ExecuteDelete();
        }

        public bool HasLink(int playerId, string provider)
        {
            using var context = new MatchBuddyContext();
            return context.ExternalLogins.Any(l => l.PlayerId == playerId && l.Provider == provider);
        }

        public void AddLink(ExternalLogin link)
        {
            using var context = new MatchBuddyContext();
            context.ExternalLogins.Add(link);
            context.SaveChanges();
        }

        public void CreatePlayerWithLink(Player player, ExternalLogin link)
        {
            using var context = new MatchBuddyContext();
            using var tx = context.Database.BeginTransaction();
            context.Players.Add(player);
            context.SaveChanges();
            link.PlayerId = player.PlayerId;
            context.ExternalLogins.Add(link);
            context.SaveChanges();
            tx.Commit();
        }

        public List<ExternalLogin> GetLinks(int playerId)
        {
            using var context = new MatchBuddyContext();
            return context.ExternalLogins.AsNoTracking()
                .Where(l => l.PlayerId == playerId)
                .OrderBy(l => l.Provider)
                .ToList();
        }

        public bool RemoveLink(int playerId, string provider)
        {
            using var context = new MatchBuddyContext();
            return context.ExternalLogins.Where(l => l.PlayerId == playerId && l.Provider == provider).ExecuteDelete() > 0;
        }

        // ------------------------------------------------------------------ KVKK: veri dışa aktarımı

        private static string MediaUrl(string keyPrefix) => $"/media/{keyPrefix}/md.webp";

        public Dictionary<string, object?> Export(int playerId)
        {
            using var context = new MatchBuddyContext();
            var p = context.Players.AsNoTracking().First(x => x.PlayerId == playerId);

            var profile = new
            {
                p.PlayerId, p.UserName, p.PlayerName, p.PlayerSurname, p.Email, p.EmailConfirmed, p.PhoneNumber, p.PhoneConfirmed,
                p.Address, p.Age, p.Size, p.Weight, p.UserScore, p.Role, p.ProfilePictureUrl, p.HasPassword,
                p.MembershipUntil, p.MembershipSuspended,
            };
            var consent = new { p.TermsVersion, p.TermsAcceptedAt, p.PrivacyAcceptedAt, p.MarketingConsent, p.MarketingConsentAt };

            var settings = context.PlayerSettings.AsNoTracking().Where(s => s.PlayerId == playerId)
                .Select(s => new
                {
                    s.MessagePermission, s.ProfileVisibility, s.NotifyFollow, s.NotifyMatchInvite, s.NotifyTeamInvite,
                    s.NotifyMatchReminder, s.NotifyMatchResult, s.NotifyMatchUpdates, s.NotifyDeals, s.Theme, s.Language,
                }).FirstOrDefault();

            var participations = (from mp in context.MatchParticipants.AsNoTracking()
                                  join m in context.Matchs on mp.MatchId equals m.MatchId
                                  join st in context.Stadiums on m.StadiumId equals st.StadiumId into sj
                                  from st in sj.DefaultIfEmpty()
                                  where mp.PlayerId == playerId
                                  orderby m.MatchDate descending
                                  select new
                                  {
                                      mp.MatchId, m.MatchName, m.MatchDate, StadiumName = st != null ? st.StadiumName : null,
                                      mp.Status, mp.Side, mp.Confirmed, mp.JoinedAt, mp.Goals, mp.Assists,
                                      m.HomeScore, m.AwayScore,
                                  }).ToList();

            var createdMatches = context.Matchs.AsNoTracking().Where(m => m.CreatedByPlayerId == playerId)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new { m.MatchId, m.MatchName, m.MatchDate, m.CreatedAt, m.IsActive, m.TotalPrice, m.UserCount })
                .ToList();

            var messagesSent = context.Messages.AsNoTracking().Where(m => m.SendPlayerId == playerId)
                .OrderBy(m => m.SendingDate)
                .Select(m => new { m.MessageId, ToPlayerId = m.RecipientPlayerId, Text = m.MatchMessage, m.ImageUrl, m.SendingDate })
                .ToList();
            var messagesReceived = context.Messages.AsNoTracking().Where(m => m.RecipientPlayerId == playerId)
                .OrderBy(m => m.SendingDate)
                .Select(m => new { m.MessageId, FromPlayerId = m.SendPlayerId, Text = m.MatchMessage, m.ImageUrl, m.SendingDate })
                .ToList();
            var groupMessagesSent = context.GroupMessages.AsNoTracking().Where(m => m.SendPlayerId == playerId)
                .OrderBy(m => m.SendingDate)
                .Select(m => new { m.GroupMessageId, m.GroupId, Text = m.MatchMessage, m.ImageUrl, m.SendingDate })
                .ToList();

            // Başkalarının kimliği gizli: alınan puanlarda puan veren gösterilmez
            var ratingsGiven = context.PlayerRatings.AsNoTracking().Where(r => r.RaterPlayerId == playerId)
                .Select(r => new { r.MatchId, r.RatedPlayerId, r.Score, r.CreatedAt }).ToList();
            var ratingsReceived = context.PlayerRatings.AsNoTracking().Where(r => r.RatedPlayerId == playerId)
                .Select(r => new { r.MatchId, r.Score, r.CreatedAt }).ToList();
            var mvpVotesGiven = context.MvpVotes.AsNoTracking().Where(v => v.VoterPlayerId == playerId)
                .Select(v => new { v.MatchId, v.VotedPlayerId, v.CreatedAt }).ToList();
            var comments = context.MatchComments.AsNoTracking().Where(c => c.playerId == playerId)
                .Select(c => new { c.CommentsId, c.MatchId, c.Comment }).ToList();

            var notifications = (from pn in context.PlayerNotification.AsNoTracking()
                                 join n in context.Notification on pn.NotificationId equals n.NotificationId
                                 where pn.PlayerId == playerId
                                 orderby pn.SentAt descending
                                 select new { n.NotificationId, n.Text, n.Type, n.MatchId, pn.IsRead, pn.SentAt }).ToList();

            var following = (from f in context.Follows.AsNoTracking()
                             join x in context.Players on f.FolloweeId equals x.PlayerId
                             where f.FollowerId == playerId
                             select new { x.PlayerId, x.UserName, f.CreatedAt }).ToList();
            var followers = (from f in context.Follows.AsNoTracking()
                             join x in context.Players on f.FollowerId equals x.PlayerId
                             where f.FolloweeId == playerId
                             select new { x.PlayerId, x.UserName, f.CreatedAt }).ToList();
            var blocked = (from b in context.Blocks.AsNoTracking()
                           join x in context.Players on b.BlockedId equals x.PlayerId
                           where b.BlockerId == playerId
                           select new { x.PlayerId, x.UserName, b.CreatedAt }).ToList();
            var reports = context.Reports.AsNoTracking().Where(r => r.ReporterId == playerId)
                .Select(r => new { r.ReportedPlayerId, r.Reason, r.Details, r.Status, r.CreatedAt }).ToList();

            var teams = (from pt in context.PlayerTeam.AsNoTracking()
                         join t in context.Teams on pt.TeamId equals t.TeamId
                         where pt.PlayerId == playerId
                         select new { t.TeamId, t.TeamName }).ToList();

            var media = context.Media.AsNoTracking()
                .Where(m => m.OwnerPlayerId == playerId && m.Status != MediaStatus.Deleted)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new { m.MediaId, m.Kind, m.KeyPrefix, m.Width, m.Height, m.CreatedAt })
                .AsEnumerable()
                .Select(m => new { m.MediaId, Kind = m.Kind.ToString(), Url = MediaUrl(m.KeyPrefix), m.Width, m.Height, m.CreatedAt })
                .ToList();

            var externalLogins = context.ExternalLogins.AsNoTracking().Where(l => l.PlayerId == playerId)
                .Select(l => new { l.Provider, l.Email, l.DisplayName, l.CreatedAt, l.LastUsedAt }).ToList();

            var nowUtc = DateTime.UtcNow;
            var sessions = context.RefreshTokens.AsNoTracking()
                .Where(t => t.PlayerId == playerId && t.RevokedAt == null && t.ExpiresAt > nowUtc)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new { t.CreatedAt, t.ExpiresAt, t.CreatedByIp, t.UserAgent, t.Persistent }).ToList();

            return new Dictionary<string, object?>
            {
                ["profile"] = profile,
                ["consent"] = consent,
                ["settings"] = settings,
                ["matchParticipations"] = participations,
                ["matchesCreated"] = createdMatches,
                ["messagesSent"] = messagesSent,
                ["messagesReceived"] = messagesReceived,
                ["groupMessagesSent"] = groupMessagesSent,
                ["matchComments"] = comments,
                ["ratingsGiven"] = ratingsGiven,
                ["ratingsReceived"] = ratingsReceived,
                ["mvpVotesGiven"] = mvpVotesGiven,
                ["notifications"] = notifications,
                ["following"] = following,
                ["followers"] = followers,
                ["blocked"] = blocked,
                ["reportsSubmitted"] = reports,
                ["teams"] = teams,
                ["media"] = media,
                ["externalLogins"] = externalLogins,
                ["activeSessions"] = sessions,
            };
        }
    }

    /// <summary>Serbest biçimli telefon numarasından son 10 hane (TR cep: 5XXXXXXXXX).</summary>
    public static class PhoneDigits
    {
        public static string Last10(string? phone)
        {
            var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
            return digits.Length >= 10 ? digits[^10..] : digits;
        }
    }
}
