using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatchBuddy.DataAccess.Concrete.EntityFramework
{
    /// <summary>
    /// Maç kadrosu iş kuralları (katıl / ayrıl / yedekten yükselt) ve aktivite yazımı.
    /// Hepsi çağıranın context'i ve transaction'ı içinde çalışır, SaveChanges çağırmaz
    /// (Join/Leave hariç: onlar da çağırmaz; çağıran kaydeder ve commit eder).
    /// EFMatchParticipationDal (Join/Leave), davet kabulü ve hesap silme aynı mantığı kullanır.
    /// </summary>
    public static class ParticipationCore
    {
        public const byte RejectedStatus = 3; // Match.IsActive: 3 = reddedildi

        public const string OwnerCannotJoin = "İşletme hesapları maça katılamaz.";

        // Bir tarafın kapasitesi: ceil(UserCount / 2)
        public static int SideCapacity(Match match) => (match.UserCount + 1) / 2;

        public static bool IsPast(Match match) =>
            match.MatchDate.HasValue && match.MatchDate.Value < DateTime.Now;

        // Maç satırını transaction boyunca kilitler: aynı maça eşzamanlı katılım/ayrılma
        // işlemleri sırayla çalışır, kapasite aşılmaz.
        public static Match? LockMatch(MatchBuddyContext context, int matchId)
        {
            return context.Matchs
                .FromSqlInterpolated($"SELECT * FROM Matchs WITH (UPDLOCK, HOLDLOCK) WHERE MatchId = {matchId}")
                .AsEnumerable()
                .FirstOrDefault();
        }

        public static bool CanManage(MatchBuddyContext context, Match match, int playerId, bool isAdmin)
        {
            if (isAdmin) return true;
            if (match.CreatedByPlayerId.HasValue) return match.CreatedByPlayerId.Value == playerId;
            // Kurucusu bilinmeyen eski maçlar: maçta Going olan oyuncu yönetebilir
            return context.MatchParticipants.Any(p =>
                p.MatchId == match.MatchId && p.PlayerId == playerId && p.Status == ParticipantStatus.Going);
        }

        // Daha az oyuncusu olan taraf (eşitse Home)
        public static byte PickSide(IEnumerable<MatchParticipant> going)
        {
            var list = going.ToList();
            var home = list.Count(p => p.Side == ParticipantSide.Home);
            var away = list.Count(p => p.Side == ParticipantSide.Away);
            return away < home ? ParticipantSide.Away : ParticipantSide.Home;
        }

        public static string DisplayName(Player? p)
        {
            if (p == null) return "Bir oyuncu";
            var name = $"{p.PlayerName} {p.PlayerSurname}".Trim();
            return string.IsNullOrWhiteSpace(name) ? (p.UserName ?? "Bir oyuncu") : name;
        }

        // Maça bağlı bildirim (tercihler NotificationWriter'da kontrol edilir)
        public static void AddNotification(MatchBuddyContext context, NotificationType type, string text, int matchId, IEnumerable<int> playerIds)
        {
            NotificationWriter.Add(context, type, text, matchId, null, playerIds);
        }

        public static void AddActivity(MatchBuddyContext context, byte type, int actorPlayerId,
            int? matchId = null, int? teamId = null, int? targetPlayerId = null, int? value = null)
        {
            context.Activities.Add(new Activity
            {
                Type = type,
                ActorPlayerId = actorPlayerId,
                MatchId = matchId,
                TeamId = teamId,
                TargetPlayerId = targetPlayerId,
                Value = value,
                CreatedAt = DateTime.Now,
            });
        }

        // Kapasite doldukça yedekleri (en eski önce) asile yükseltir, bildirim ve aktivite yazar.
        // participants: maçın (takip edilen) tüm kayıtları, güncel durumlarıyla.
        public static void PromoteFromWaitlist(MatchBuddyContext context, Match match, List<MatchParticipant> participants, byte? preferredSide)
        {
            if (match.IsActive == RejectedStatus || IsPast(match)) return;

            while (true)
            {
                var going = participants.Where(p => p.Status == ParticipantStatus.Going).ToList();
                if (going.Count >= match.UserCount) break;

                var next = participants
                    .Where(p => p.Status == ParticipantStatus.Waitlist)
                    .OrderBy(p => p.JoinedAt)
                    .ThenBy(p => p.MatchParticipantId)
                    .FirstOrDefault();
                if (next == null) break;

                next.Status = ParticipantStatus.Going;
                next.Side = preferredSide ?? PickSide(going);
                next.Confirmed = false;
                next.ReminderSentAt = null;
                preferredSide = null; // boşalan taraf sadece ilk yükselen için

                AddNotification(context, NotificationType.WaitlistPromoted,
                    $"Yedek listesinden asil kadroya geçtin: '{match.MatchName}' maçı, {match.MatchDate:dd.MM.yyyy HH:mm}.",
                    match.MatchId, new[] { next.PlayerId });
                AddActivity(context, ActivityType.JoinedMatch, next.PlayerId, matchId: match.MatchId);
            }
        }

        /// <summary>
        /// Katıl mantığı. match: LockMatch ile kilitlenmiş satır. SaveChanges çağırmaz.
        /// Going olursa JoinedMatch aktivitesi yazılır, kurucuya bildirim gider.
        /// </summary>
        public static OperationResult<byte> Join(MatchBuddyContext context, Match match, int playerId, byte? preferredSide = null)
        {
            // İşletme hesapları maça katılamaz (docs/venues-api.md)
            if (context.Players.Any(p => p.PlayerId == playerId && p.Role == PlayerRoles.Owner))
                return OperationResult<byte>.Forbidden(OwnerCannotJoin);
            if (match.IsActive == RejectedStatus)
                return OperationResult<byte>.BadRequest("Bu maç reddedildi, katılım kapalı.");
            if (IsPast(match))
                return OperationResult<byte>.BadRequest("Maç tarihi geçti, katılım kapalı.");

            var matchId = match.MatchId;
            var participants = context.MatchParticipants.Where(p => p.MatchId == matchId).ToList();
            var mine = participants.FirstOrDefault(p => p.PlayerId == playerId);
            if (mine?.Status == ParticipantStatus.Going)
                return OperationResult<byte>.BadRequest("Bu maça zaten katılıyorsun.");
            if (mine?.Status == ParticipantStatus.Waitlist)
                return OperationResult<byte>.BadRequest("Zaten bu maçın yedek listesindesin.");

            var going = participants.Where(p => p.Status == ParticipantStatus.Going).ToList();
            byte status;
            byte? side;
            if (going.Count < match.UserCount)
            {
                status = ParticipantStatus.Going;
                side = PickSide(going);
                // Takım daveti / rakip takım: tercih edilen taraf doluysa normal kural (PickSide)
                if (preferredSide.HasValue && going.Count(p => p.Side == preferredSide.Value) < SideCapacity(match))
                {
                    side = preferredSide.Value;
                }
            }
            else
            {
                status = ParticipantStatus.Waitlist;
                side = null;
            }

            if (mine == null)
            {
                mine = new MatchParticipant { MatchId = matchId, PlayerId = playerId };
                context.MatchParticipants.Add(mine);
            }
            // Daha önce "gelemiyor" olan kayıt yeniden kullanılır
            mine.Status = status;
            mine.Side = side;
            mine.Confirmed = false;
            mine.JoinedAt = DateTime.Now;
            mine.ReminderSentAt = null;
            mine.Goals = 0;
            mine.Assists = 0;

            if (status == ParticipantStatus.Going)
            {
                AddActivity(context, ActivityType.JoinedMatch, playerId, matchId: matchId);
            }

            // Kurucuya bildirim
            if (match.CreatedByPlayerId.HasValue && match.CreatedByPlayerId.Value != playerId)
            {
                var player = context.Players.AsNoTracking().FirstOrDefault(p => p.PlayerId == playerId);
                var text = status == ParticipantStatus.Going
                    ? $"{DisplayName(player)}, '{match.MatchName}' maçına katıldı."
                    : $"{DisplayName(player)}, '{match.MatchName}' maçının yedek listesine katıldı.";
                AddNotification(context, NotificationType.MatchJoin, text, matchId, new[] { match.CreatedByPlayerId.Value });
            }

            return OperationResult<byte>.Ok(status,
                status == ParticipantStatus.Going ? "Maça katıldın." : "Maç dolu, yedek listesine eklendin.");
        }

        /// <summary>
        /// Ayrıl mantığı (Going ayrılırsa yedekten yükseltme dahil). match: kilitli satır. SaveChanges çağırmaz.
        /// </summary>
        public static OperationResult<bool> Leave(MatchBuddyContext context, Match match, int playerId)
        {
            var participants = context.MatchParticipants.Where(p => p.MatchId == match.MatchId).ToList();
            var mine = participants.FirstOrDefault(p => p.PlayerId == playerId);
            if (mine == null || mine.Status == ParticipantStatus.Declined)
                return OperationResult<bool>.BadRequest("Bu maçta kaydın bulunmuyor.");
            if (IsPast(match))
                return OperationResult<bool>.BadRequest("Maç tarihi geçti, kadrodan ayrılamazsın.");

            var wasGoing = mine.Status == ParticipantStatus.Going;
            var vacatedSide = mine.Side;

            mine.Status = ParticipantStatus.Declined;
            mine.Side = null;
            mine.Confirmed = false;
            mine.ReminderSentAt = null;

            if (wasGoing)
            {
                PromoteFromWaitlist(context, match, participants, vacatedSide);
            }

            return OperationResult<bool>.Ok(true, wasGoing ? "Maçtan ayrıldın." : "Yedek listesinden çıktın.");
        }
    }
}
