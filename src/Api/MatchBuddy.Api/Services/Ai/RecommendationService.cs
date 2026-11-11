using MatchBuddy.DataAccess;
using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatchBuddy.Api.Services.Ai
{
    /// <summary>Önerilen maç: puan + kullanıcıya gösterilecek gerekçe (frontend home:ai.reasons.&lt;code&gt; ile çevirir).</summary>
    public sealed record MatchRecommendation(int MatchId, double Score, string Reason, Dictionary<string, object> Args);

    public sealed record BalancedPlayer(int PlayerId, string? PlayerName, string? PlayerSurname, string? UserName,
        string? ProfilePictureUrl, int Age, int Size, int Weight, int UserScore, double Rating, double Power);

    public sealed record BalancedTeams(List<BalancedPlayer> Team1, List<BalancedPlayer> Team2, double Power1, double Power2);

    /// <summary>
    /// Eski Python servislerinin (ai_match_sorter.py, GetAPIPlayers.py) yerine API içinde çalışan öneri ve takım dengeleme.
    /// Ayrı servis / ayrı port gerekmez; aynı JWT ve aynı veritabanı kullanılır — docs/ai-api.md.
    /// </summary>
    public sealed class RecommendationService : IDisposable
    {
        private const int MaxCandidates = 200;
        private readonly MatchBuddyContext _db;

        // Projede DbContext DI'a kayıtlı değil; DAL'lar gibi kendi bağlamını açar (scoped → istek sonunda kapanır)
        public RecommendationService() => _db = new MatchBuddyContext();

        public void Dispose() => _db.Dispose();

        // ================================================================== Maç önerisi

        /// <summary>
        /// Oyuncu için katılabileceği yaklaşan maçları sıralar. Sinyaller (ağırlık):
        /// tarih yakınlığı (0.35), tanıdık oyuncular — takip ettikleri ya da birlikte oynadıkları (0.25),
        /// seviye uyumu (0.15), tesis tanıdıklığı (0.10), doluluk / popülerlik (0.15). Dolu ve iptal maçlar atlanır.
        /// </summary>
        public async Task<List<MatchRecommendation>> RecommendMatchesAsync(int playerId, CancellationToken ct)
        {
            var now = DateTime.Now;
            var me = await _db.Players.AsNoTracking().Where(p => p.PlayerId == playerId)
                .Select(p => new { p.PlayerId, p.UserScore }).FirstOrDefaultAsync(ct);
            if (me == null) return new();

            var candidates = await _db.Matchs.AsNoTracking()
                .Where(m => m.MatchDate != null && m.MatchDate > now && m.IsActive != 3 && m.CreatedByPlayerId != playerId)
                .Where(m => !_db.MatchParticipants.Any(p => p.MatchId == m.MatchId && p.PlayerId == playerId && p.Status != ParticipantStatus.Declined))
                .OrderBy(m => m.MatchDate)
                .Take(MaxCandidates)
                .Select(m => new
                {
                    m.MatchId, m.MatchDate, m.UserCount, m.Likes, m.StadiumId,
                    StadiumName = m.Stadium.StadiumName,
                    Going = _db.MatchParticipants.Where(p => p.MatchId == m.MatchId && p.Status == ParticipantStatus.Going)
                        .Select(p => new { p.PlayerId, p.Player.UserScore }).ToList(),
                    Guests = _db.MatchGuests.Count(g => g.MatchId == m.MatchId),
                })
                .ToListAsync(ct);
            if (candidates.Count == 0) return new();

            // Tanıdıklar: takip ettiklerim + son 50 maçımda birlikte oynadıklarım
            var following = await _db.Follows.AsNoTracking().Where(f => f.FollowerId == playerId)
                .Select(f => f.FolloweeId).ToListAsync(ct);
            var myPastMatchIds = await _db.MatchParticipants.AsNoTracking()
                .Where(p => p.PlayerId == playerId && p.Status == ParticipantStatus.Going && p.Match.MatchDate < now)
                .OrderByDescending(p => p.Match.MatchDate).Select(p => p.MatchId).Take(50).ToListAsync(ct);
            var teammates = await _db.MatchParticipants.AsNoTracking()
                .Where(p => myPastMatchIds.Contains(p.MatchId) && p.PlayerId != playerId && p.Status == ParticipantStatus.Going)
                .Select(p => p.PlayerId).Distinct().ToListAsync(ct);
            var known = following.Concat(teammates).ToHashSet();
            var venueCounts = await _db.MatchParticipants.AsNoTracking()
                .Where(p => myPastMatchIds.Contains(p.MatchId))
                .GroupBy(p => p.Match.StadiumId).Select(g => new { StadiumId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.StadiumId, x => x.Count, ct);

            var list = new List<MatchRecommendation>();
            foreach (var m in candidates)
            {
                var going = m.Going.Count + m.Guests;
                var capacity = m.UserCount > 0 ? m.UserCount : 10;
                if (going >= capacity) continue; // yer yok

                var days = Math.Max(0, (m.MatchDate!.Value - now).TotalDays);
                var dateScore = days <= 14 ? 100 * (1 - days / 14) : days <= 30 ? 50 * (1 - (days - 14) / 16) : 50 / (1 + days / 30);

                var knownGoing = m.Going.Count(p => known.Contains(p.PlayerId));
                var knownScore = Math.Min(knownGoing * 35, 100);

                var avg = m.Going.Count > 0 ? m.Going.Average(p => p.UserScore) : me.UserScore;
                var levelScore = 100 - Math.Min(Math.Abs(avg - me.UserScore), 100);

                venueCounts.TryGetValue(m.StadiumId, out var venueCount);
                var venueScore = Math.Min(venueCount * 25, 100);

                var fill = (double)going / capacity;
                var popularity = Math.Min(fill * 80 + m.Likes * 2, 100);

                var total = 0.35 * dateScore + 0.25 * knownScore + 0.15 * levelScore + 0.10 * venueScore + 0.15 * popularity;

                // Gerekçe: en güçlü, kullanıcıya anlamlı sinyal
                var spots = capacity - going;
                string reason;
                var args = new Dictionary<string, object>();
                if (knownGoing > 0) { reason = "friends"; args["count"] = knownGoing; }
                else if (spots <= 2) { reason = "lastSpots"; args["count"] = spots; }
                else if (venueCount > 0) { reason = "venue"; args["venue"] = m.StadiumName ?? string.Empty; args["count"] = venueCount; }
                else if (days <= 2) { reason = "soon"; }
                else if (m.Going.Count >= 3 && levelScore >= 85) { reason = "level"; }
                else if (fill >= 0.5) { reason = "popular"; args["count"] = going; }
                else { reason = "open"; args["count"] = spots; }

                list.Add(new MatchRecommendation(m.MatchId, Math.Round(total, 1), reason, args));
            }
            return list.OrderByDescending(r => r.Score).ToList();
        }

        // ================================================================== Takım dengeleme

        /// <summary>
        /// Oyuncuları iki dengeli takıma böler. Güç = maç sonrası oyuncu puanı ortalaması (1–5 → 0–100, yoksa UserScore)
        /// ağırlıklı, yaş/boy küçük katkı. Güçlüden zayıfa sıralanır, her oyuncu toplam gücü düşük (ve kadrosu eksik) takıma gider;
        /// ardından takımlar arası tek takas ile fark küçültülür.
        /// </summary>
        public async Task<BalancedTeams> BalanceAsync(IReadOnlyCollection<int> playerIds, CancellationToken ct)
        {
            var ids = playerIds.Distinct().ToList();
            var players = await _db.Players.AsNoTracking().Where(p => ids.Contains(p.PlayerId))
                .Select(p => new
                {
                    p.PlayerId, p.PlayerName, p.PlayerSurname, p.UserName, p.ProfilePictureUrl, p.Age, p.Size, p.Weight, p.UserScore,
                    Rating = _db.PlayerRatings.Where(r => r.RatedPlayerId == p.PlayerId).Average(r => (double?)r.Score),
                })
                .ToListAsync(ct);

            var scored = players.Select(p =>
            {
                var rating = p.Rating ?? 0;
                var skill = p.Rating.HasValue ? (rating - 1) / 4 * 100 : Math.Clamp(p.UserScore, 0, 100);
                var age = p.Age is > 0 and < 80 ? Math.Clamp(1 - Math.Abs(p.Age - 26) / 30.0, 0, 1) * 100 : 50;
                var height = p.Size is > 120 and < 230 ? Math.Clamp((p.Size - 150) / 50.0, 0, 1) * 100 : 50;
                var power = Math.Round(0.75 * skill + 0.15 * age + 0.10 * height, 1);
                return new BalancedPlayer(p.PlayerId, p.PlayerName, p.PlayerSurname, p.UserName, p.ProfilePictureUrl,
                    p.Age, p.Size, p.Weight, p.UserScore, Math.Round(rating, 2), power);
            })
            // Eşit güçte rastgelelik: "yeniden oluştur" farklı bir dağılım verebilsin
            .OrderByDescending(p => p.Power).ThenBy(_ => Random.Shared.Next())
            .ToList();

            var t1 = new List<BalancedPlayer>();
            var t2 = new List<BalancedPlayer>();
            var half = (scored.Count + 1) / 2;
            foreach (var p in scored)
            {
                var to1 = t1.Count < half && (t2.Count >= half || t1.Sum(x => x.Power) <= t2.Sum(x => x.Power));
                (to1 ? t1 : t2).Add(p);
            }

            // Tek takasla iyileştirme
            for (var iter = 0; iter < 20; iter++)
            {
                var diff = t1.Sum(x => x.Power) - t2.Sum(x => x.Power);
                (BalancedPlayer a, BalancedPlayer b, double gain)? best = null;
                foreach (var a in t1)
                    foreach (var b in t2)
                    {
                        var newDiff = Math.Abs(diff - 2 * (a.Power - b.Power));
                        var gain = Math.Abs(diff) - newDiff;
                        if (gain > 0.01 && (best == null || gain > best.Value.gain)) best = (a, b, gain);
                    }
                if (best == null) break;
                t1.Remove(best.Value.a); t2.Remove(best.Value.b);
                t1.Add(best.Value.b); t2.Add(best.Value.a);
            }

            return new BalancedTeams(t1.OrderByDescending(p => p.Power).ToList(), t2.OrderByDescending(p => p.Power).ToList(),
                Math.Round(t1.Sum(x => x.Power), 1), Math.Round(t2.Sum(x => x.Power), 1));
        }

        /// <summary>Maçta "katılıyor" olan oyuncular (misafirler hariç).</summary>
        public Task<List<int>> GoingPlayerIdsAsync(int matchId, CancellationToken ct) =>
            _db.MatchParticipants.AsNoTracking()
                .Where(p => p.MatchId == matchId && p.Status == ParticipantStatus.Going)
                .Select(p => p.PlayerId).ToListAsync(ct);

        /// <summary>Takım oluşturma ekranının varsayılan havuzu: ben + takip ettiklerim.</summary>
        public async Task<List<int>> FriendPoolAsync(int playerId, CancellationToken ct)
        {
            var ids = await _db.Follows.AsNoTracking().Where(f => f.FollowerId == playerId)
                .Select(f => f.FolloweeId).ToListAsync(ct);
            ids.Add(playerId);
            return ids;
        }

        public Task<Match?> FindMatchAsync(int matchId, CancellationToken ct) =>
            _db.Matchs.AsNoTracking().FirstOrDefaultAsync(m => m.MatchId == matchId, ct);

        public Task<bool> IsParticipantAsync(int matchId, int playerId, CancellationToken ct) =>
            _db.MatchParticipants.AnyAsync(p => p.MatchId == matchId && p.PlayerId == playerId && p.Status == ParticipantStatus.Going, ct);

        /// <summary>Dengelenmiş takımları maçın taraflarına (ev / deplasman) yazar.</summary>
        public async Task ApplySidesAsync(int matchId, BalancedTeams teams, CancellationToken ct)
        {
            var home = teams.Team1.Select(p => p.PlayerId).ToHashSet();
            var away = teams.Team2.Select(p => p.PlayerId).ToHashSet();
            var rows = await _db.MatchParticipants.Where(p => p.MatchId == matchId && p.Status == ParticipantStatus.Going).ToListAsync(ct);
            foreach (var r in rows)
                r.Side = home.Contains(r.PlayerId) ? ParticipantSide.Home : away.Contains(r.PlayerId) ? ParticipantSide.Away : r.Side;
            await _db.SaveChangesAsync(ct);
        }
    }
}
