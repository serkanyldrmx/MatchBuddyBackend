using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatcBuddy.DataAccess.Concrete.EntityFramework
{
    /// <summary>Oyuncu rozeti: puan + seviye + tamamlanan maç sayısı.</summary>
    public sealed record ReliabilityBadge(int Score, string Level, int CompletedMatches);

    /// <summary>Maçtan çekilmenin cezası. Kind: "none" | "late" | "veryLate".</summary>
    public sealed record LeavePenalty(string Kind, byte? Type, int Penalty, double HoursLeft);

    /// <summary>
    /// Güvenilirlik puanı kuralları (docs/reliability-api.md). Tüm sayılar SystemParameters (grup Reliability).
    /// Aynı DbContext/transaction içinde çağrılır (katılım işlemleriyle birlikte atomik).
    /// </summary>
    public static class ReliabilityCore
    {
        public const int StartScore = 100;

        /// <summary>Maça kalan süreye göre çekilme cezası. Yalnızca "katılıyor" durumundaki oyuncu için anlamlıdır.</summary>
        public static LeavePenalty PenaltyFor(Match match, DateTime now)
        {
            if (match.MatchDate == null) return new("none", null, 0, double.MaxValue);
            var hours = (match.MatchDate.Value - now).TotalHours;
            if (hours < P.Int(ParameterKeys.Reliability.VeryLateCancelHours))
                return new("veryLate", ReliabilityEventType.VeryLateCancel, P.Int(ParameterKeys.Reliability.VeryLateCancelPenalty), hours);
            if (hours < P.Int(ParameterKeys.Reliability.LateCancelHours))
                return new("late", ReliabilityEventType.LateCancel, P.Int(ParameterKeys.Reliability.LateCancelPenalty), hours);
            return new("none", null, 0, hours);
        }

        public static ReliabilityEvent AddEvent(MatcBuddyContext context, int playerId, int? matchId, byte type, int delta,
            string? reasonCode = null, string? note = null)
        {
            var e = new ReliabilityEvent
            {
                PlayerId = playerId,
                MatchId = matchId,
                Type = type,
                Delta = delta,
                ReasonCode = reasonCode,
                Note = note,
                CreatedAt = DateTime.Now,
            };
            context.ReliabilityEvents.Add(e);
            return e;
        }

        /// <summary>
        /// Puanı olaylardan yeniden hesaplar ve Players.ReliabilityScore önbelleğini günceller.
        /// Bekleyen (kaydedilmemiş) olaylar için önce SaveChanges çağrılmalıdır.
        /// </summary>
        public static int Recompute(MatcBuddyContext context, int playerId)
        {
            var deltas = context.ReliabilityEvents.AsNoTracking()
                .Where(e => e.PlayerId == playerId && e.ReversedAt == null)
                .OrderBy(e => e.CreatedAt).ThenBy(e => e.ReliabilityEventId)
                .Select(e => e.Delta).ToList();
            var score = StartScore;
            foreach (var d in deltas) score = Math.Clamp(score + d, 0, 100);

            var player = context.Players.Local.FirstOrDefault(p => p.PlayerId == playerId)
                         ?? context.Players.FirstOrDefault(p => p.PlayerId == playerId);
            if (player != null && player.ReliabilityScore != score)
            {
                player.ReliabilityScore = score;
            }
            return score;
        }

        public static string LevelOf(int score, int completedMatches)
        {
            if (completedMatches < P.Int(ParameterKeys.Reliability.NewPlayerMatchCount)) return ReliabilityLevel.New;
            if (score >= P.Int(ParameterKeys.Reliability.LevelGood)) return ReliabilityLevel.Good;
            if (score >= P.Int(ParameterKeys.Reliability.LevelMedium)) return ReliabilityLevel.Medium;
            return ReliabilityLevel.Risky;
        }

        /// <summary>Tamamlanan maç: maç sonrası işlenmiş (Attended ya da NoShow) maç sayısı.</summary>
        public static Dictionary<int, int> CompletedCounts(MatcBuddyContext context, IReadOnlyCollection<int> playerIds)
        {
            if (playerIds.Count == 0) return new();
            return context.ReliabilityEvents.AsNoTracking()
                .Where(e => playerIds.Contains(e.PlayerId)
                            && (e.Type == ReliabilityEventType.Attended || e.Type == ReliabilityEventType.NoShow))
                .GroupBy(e => e.PlayerId)
                .Select(g => new { g.Key, Count = g.Select(x => x.MatchId).Distinct().Count() })
                .ToDictionary(x => x.Key, x => x.Count);
        }

        /// <summary>Birden çok oyuncunun rozeti (katılımcı listeleri için tek sorgu).</summary>
        public static Dictionary<int, ReliabilityBadge> Badges(MatcBuddyContext context, IEnumerable<int> playerIds)
        {
            var ids = playerIds.Distinct().ToList();
            if (ids.Count == 0) return new();
            var scores = context.Players.AsNoTracking().Where(p => ids.Contains(p.PlayerId))
                .Select(p => new { p.PlayerId, p.ReliabilityScore }).ToDictionary(x => x.PlayerId, x => x.ReliabilityScore);
            var completed = CompletedCounts(context, ids);
            return ids.ToDictionary(id => id, id =>
            {
                var score = scores.TryGetValue(id, out var s) ? s : StartScore;
                var done = completed.TryGetValue(id, out var c) ? c : 0;
                return new ReliabilityBadge(score, LevelOf(score, done), done);
            });
        }

        public static ReliabilityBadge Badge(MatcBuddyContext context, int playerId) =>
            Badges(context, new[] { playerId })[playerId];

        /// <summary>
        /// Maç sonrası: "gelmedi" işaretleme penceresi kapanan maçlarda, katılan ve "gelmedi" işaretlenmeyen oyunculara
        /// Attended olayı yazılır. Reddedilmiş (IsActive = 3) maçlar atlanır. Döner: işlenen maç sayısı.
        /// </summary>
        public static int SettleDue(MatcBuddyContext context, int batch = 50)
        {
            var window = P.Int(ParameterKeys.Reliability.NoShowMarkWindowHours);
            var bonus = P.Int(ParameterKeys.Reliability.AttendBonus);
            var cutoff = DateTime.Now.AddHours(-window);

            var matches = context.Matchs
                .Where(m => m.ReliabilitySettledAt == null && m.IsActive != ParticipationCore.RejectedStatus
                            && m.MatchDate != null && m.MatchDate < cutoff)
                .OrderBy(m => m.MatchDate).Take(batch).ToList();
            if (matches.Count == 0) return 0;

            var matchIds = matches.Select(m => m.MatchId).ToList();
            var going = context.MatchParticipants.AsNoTracking()
                .Where(p => matchIds.Contains(p.MatchId) && p.Status == ParticipantStatus.Going)
                .Select(p => new { p.MatchId, p.PlayerId }).ToList();
            var marked = context.ReliabilityEvents.AsNoTracking()
                .Where(e => e.MatchId != null && matchIds.Contains(e.MatchId.Value)
                            && (e.Type == ReliabilityEventType.NoShow || e.Type == ReliabilityEventType.Attended))
                .Select(e => new { MatchId = e.MatchId!.Value, e.PlayerId }).ToList()
                .Select(x => (x.MatchId, x.PlayerId)).ToHashSet();

            var touched = new HashSet<int>();
            foreach (var m in matches)
            {
                foreach (var p in going.Where(g => g.MatchId == m.MatchId))
                {
                    if (marked.Contains((m.MatchId, p.PlayerId))) continue;
                    var e = AddEvent(context, p.PlayerId, m.MatchId, ReliabilityEventType.Attended, bonus);
                    // Olay maç saatine tarihlenir: puan geçmişi kronolojik kalır
                    e.CreatedAt = m.MatchDate!.Value.AddMinutes(Math.Max(m.DurationMinutes, 0));
                    touched.Add(p.PlayerId);
                }
                m.ReliabilitySettledAt = DateTime.Now;
            }
            context.SaveChanges();
            foreach (var id in touched) Recompute(context, id);
            context.SaveChanges();
            return matches.Count;
        }
    }
}
