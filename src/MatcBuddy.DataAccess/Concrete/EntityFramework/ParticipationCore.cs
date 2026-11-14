using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatcBuddy.DataAccess.Concrete.EntityFramework
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

        public const string OwnerCannotJoin = ErrorCodes.Match.OwnerCannotJoin;

        // Bir tarafın kapasitesi: ceil(UserCount / 2)
        public static int SideCapacity(Match match) => (match.UserCount + 1) / 2;

        public static bool IsPast(Match match) =>
            match.MatchDate.HasValue && match.MatchDate.Value < DateTime.Now;

        // Maç satırını transaction boyunca kilitler: aynı maça eşzamanlı katılım/ayrılma
        // işlemleri sırayla çalışır, kapasite aşılmaz.
        public static Match? LockMatch(MatcBuddyContext context, int matchId)
        {
            return context.Matchs
                .FromSqlInterpolated($"SELECT * FROM Matchs WITH (UPDLOCK, HOLDLOCK) WHERE MatchId = {matchId}")
                .AsEnumerable()
                .FirstOrDefault();
        }

        public static bool CanManage(MatcBuddyContext context, Match match, int playerId, bool isAdmin)
        {
            if (isAdmin) return true;
            if (match.CreatedByPlayerId.HasValue) return match.CreatedByPlayerId.Value == playerId;
            // Kurucusu bilinmeyen eski maçlar: maçta Going olan oyuncu yönetebilir
            return context.MatchParticipants.Any(p =>
                p.MatchId == match.MatchId && p.PlayerId == playerId && p.Status == ParticipantStatus.Going);
        }

        /// <summary>
        /// Sahadaki dizilişi (yer / taraf) düzenleyebilir mi: yönetici ya da kadroda (Going) olan herhangi bir oyuncu.
        /// </summary>
        public static bool CanArrange(MatcBuddyContext context, Match match, int playerId, bool isAdmin) =>
            CanManage(context, match, playerId, isAdmin)
            || context.MatchParticipants.Any(p =>
                p.MatchId == match.MatchId && p.PlayerId == playerId && p.Status == ParticipantStatus.Going);

        // ---- Misafir oyuncular (docs/match-participation-api.md "Ek: Misafir oyuncular") ----
        // Misafirler kadroya sayılır: toplam = Going + misafir, taraf = o taraftaki Going + misafir.

        public const string SideFull = ErrorCodes.Match.SideFull;

        public static List<MatchGuest> LoadGuests(MatcBuddyContext context, int matchId) =>
            context.MatchGuests.Where(g => g.MatchId == matchId).ToList();

        // Bir taraftaki kadro sayısı (Going oyuncular + misafirler)
        public static int SideCount(IEnumerable<MatchParticipant> going, IEnumerable<MatchGuest> guests, byte side) =>
            going.Count(p => p.Side == side) + guests.Count(g => g.Side == side);

        // Maç başına kadro sayısı: Going + misafir (liste ekranları için)
        public static Dictionary<int, int> RosterCounts(MatcBuddyContext context, List<int> matchIds)
        {
            var counts = context.MatchParticipants.AsNoTracking()
                .Where(p => matchIds.Contains(p.MatchId) && p.Status == ParticipantStatus.Going)
                .GroupBy(p => p.MatchId)
                .Select(g => new { MatchId = g.Key, Count = g.Count() })
                .ToList()
                .ToDictionary(x => x.MatchId, x => x.Count);
            foreach (var g in GuestCounts(context, matchIds))
            {
                counts[g.Key] = (counts.TryGetValue(g.Key, out var c) ? c : 0) + g.Value;
            }
            return counts;
        }

        // Maç başına misafir sayısı (matchIds null ise tüm maçlar)
        public static Dictionary<int, int> GuestCounts(MatcBuddyContext context, List<int>? matchIds)
        {
            var query = context.MatchGuests.AsNoTracking().AsQueryable();
            if (matchIds != null) query = query.Where(g => matchIds.Contains(g.MatchId));
            return query
                .GroupBy(g => g.MatchId)
                .Select(g => new { MatchId = g.Key, Count = g.Count() })
                .ToList()
                .ToDictionary(x => x.MatchId, x => x.Count);
        }

        // Daha az kişisi (Going + misafir) olan taraf (eşitse Home)
        public static byte PickSide(IEnumerable<MatchParticipant> going, IEnumerable<MatchGuest>? guests = null)
        {
            var list = going.ToList();
            var guestList = guests?.ToList() ?? new List<MatchGuest>();
            var home = SideCount(list, guestList, ParticipantSide.Home);
            var away = SideCount(list, guestList, ParticipantSide.Away);
            return away < home ? ParticipantSide.Away : ParticipantSide.Home;
        }

        public static string DisplayName(Player? p)
        {
            // Bildirim metinlerine (veritabanı) yazılır: varsayılan dil
            if (p == null) return Msg.GetDefault(MessageKeys.Match.LabelSomePlayer);
            var name = $"{p.PlayerName} {p.PlayerSurname}".Trim();
            return string.IsNullOrWhiteSpace(name) ? (p.UserName ?? Msg.GetDefault(MessageKeys.Match.LabelSomePlayer)) : name;
        }

        // Maça bağlı bildirim (tercihler NotificationWriter'da kontrol edilir)
        public static void AddNotification(MatcBuddyContext context, NotificationType type, string text, int matchId, IEnumerable<int> playerIds)
        {
            NotificationWriter.Add(context, type, text, matchId, null, playerIds);
        }

        public static void AddActivity(MatcBuddyContext context, byte type, int actorPlayerId,
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
        // guests: maçın güncel misafirleri (null ise veritabanından okunur). Misafirler kapasiteye sayılır.
        public static void PromoteFromWaitlist(MatcBuddyContext context, Match match, List<MatchParticipant> participants, byte? preferredSide,
            List<MatchGuest>? guests = null)
        {
            if (match.IsActive == RejectedStatus || IsPast(match)) return;
            guests ??= LoadGuests(context, match.MatchId);

            while (true)
            {
                var going = participants.Where(p => p.Status == ParticipantStatus.Going).ToList();
                if (going.Count + guests.Count >= match.UserCount) break;

                var next = participants
                    .Where(p => p.Status == ParticipantStatus.Waitlist)
                    .OrderBy(p => p.JoinedAt)
                    .ThenBy(p => p.MatchParticipantId)
                    .FirstOrDefault();
                if (next == null) break;

                next.Status = ParticipantStatus.Going;
                // Boşalan taraf tercih edilir (doluysa daha az kişili taraf)
                next.Side = preferredSide.HasValue && SideCount(going, guests, preferredSide.Value) < SideCapacity(match)
                    ? preferredSide.Value
                    : PickSide(going, guests);
                next.Confirmed = false;
                next.ReminderSentAt = null;
                preferredSide = null; // boşalan taraf sadece ilk yükselen için

                AddNotification(context, NotificationType.WaitlistPromoted,
                    Msg.GetDefault(MessageKeys.Match.NotifWaitlistPromoted, match.MatchName, match.MatchDate),
                    match.MatchId, new[] { next.PlayerId });
                AddActivity(context, ActivityType.JoinedMatch, next.PlayerId, matchId: match.MatchId);
            }
        }

        /// <summary>
        /// Katıl mantığı. match: LockMatch ile kilitlenmiş satır. SaveChanges çağırmaz.
        /// Going olursa JoinedMatch aktivitesi yazılır, kurucuya bildirim gider.
        /// </summary>
        /// <param name="checkReliability">Maçın en düşük güvenilirlik sınırı uygulanır mı (davetle / rakip takım olarak katılımda hayır).</param>
        public static OperationResult<byte> Join(MatcBuddyContext context, Match match, int playerId, byte? preferredSide = null,
            bool checkReliability = false, int? preferredSlot = null)
        {
            // İşletme hesapları maça katılamaz (docs/venues-api.md)
            if (context.Players.Any(p => p.PlayerId == playerId && p.Role == PlayerRoles.Owner))
                return OperationResult<byte>.Forbidden(OwnerCannotJoin);
            if (match.IsActive == RejectedStatus)
                return OperationResult<byte>.BadRequest(ErrorCodes.Match.RejectedJoinClosed);
            if (IsPast(match))
                return OperationResult<byte>.BadRequest(ErrorCodes.Match.PastJoinClosed);

            // Kurucunun güvenilirlik sınırı (docs/reliability-api.md): yeni oyuncular takılmaz
            if (checkReliability && match.MinReliability is int min && min > 0 && match.CreatedByPlayerId != playerId)
            {
                var badge = ReliabilityCore.Badge(context, playerId);
                if (badge.Level != ReliabilityLevel.New && badge.Score < min)
                    return OperationResult<byte>.BadRequest(ErrorCodes.Reliability.TooLow, min, badge.Score);
            }

            var matchId = match.MatchId;
            var participants = context.MatchParticipants.Where(p => p.MatchId == matchId).ToList();
            var mine = participants.FirstOrDefault(p => p.PlayerId == playerId);
            if (mine?.Status == ParticipantStatus.Going)
                return OperationResult<byte>.BadRequest(ErrorCodes.Match.AlreadyJoined);
            if (mine?.Status == ParticipantStatus.Waitlist)
                return OperationResult<byte>.BadRequest(ErrorCodes.Match.AlreadyWaitlisted);

            var going = participants.Where(p => p.Status == ParticipantStatus.Going).ToList();
            var guests = LoadGuests(context, matchId);
            byte status;
            byte? side;
            int? slot = null;
            // Kapasite: Going + misafir < UserCount (misafir yüzünden dolu maça katılan yedeğe düşer)
            if (going.Count + guests.Count < match.UserCount)
            {
                status = ParticipantStatus.Going;
                side = PickSide(going, guests);
                // Takım daveti / rakip takım: tercih edilen taraf doluysa normal kural (PickSide)
                if (preferredSide.HasValue && SideCount(going, guests, preferredSide.Value) < SideCapacity(match))
                {
                    side = preferredSide.Value;
                    // Sahada dokunulan boş yer: başkası o yeri tutmuyorsa oraya yerleşir
                    if (preferredSlot is int ps && ps >= 0 && ps < 50
                        && !going.Any(p => p.Side == side && p.Slot == ps)
                        && !guests.Any(g => g.Side == side && g.Slot == ps))
                    {
                        slot = ps;
                    }
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
            mine.Slot = slot;
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
                    ? Msg.GetDefault(MessageKeys.Match.NotifJoined, DisplayName(player), match.MatchName)
                    : Msg.GetDefault(MessageKeys.Match.NotifJoinedWaitlist, DisplayName(player), match.MatchName);
                AddNotification(context, NotificationType.MatchJoin, text, matchId, new[] { match.CreatedByPlayerId.Value });
            }

            return OperationResult<byte>.Ok(status,
                Msg.Get(status == ParticipantStatus.Going ? MessageKeys.Match.Joined : MessageKeys.Match.JoinedWaitlist));
        }

        /// <summary>
        /// Ayrıl mantığı (Going ayrılırsa yedekten yükseltme dahil). match: kilitli satır. SaveChanges çağırmaz.
        /// </summary>
        public static OperationResult<bool> Leave(MatcBuddyContext context, Match match, int playerId)
        {
            var participants = context.MatchParticipants.Where(p => p.MatchId == match.MatchId).ToList();
            var mine = participants.FirstOrDefault(p => p.PlayerId == playerId);
            if (mine == null || mine.Status == ParticipantStatus.Declined)
                return OperationResult<bool>.BadRequest(ErrorCodes.Match.NotParticipant);
            if (IsPast(match))
                return OperationResult<bool>.BadRequest(ErrorCodes.Match.PastCannotLeave);

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

            return OperationResult<bool>.Ok(true, Msg.Get(wasGoing ? MessageKeys.Match.Left : MessageKeys.Match.LeftWaitlist));
        }
    }
}
