using System.Globalization;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.DataAccess.Concrete
{
    /// <summary>
    /// Turnuva fikstür / ağaç / puan tablosu mantığı. Veritabanına dokunmaz; yalnızca bellekteki
    /// TournamentFixture nesneleri üzerinde çalışır (birim testi kolay olsun diye ayrı tutuldu).
    /// Eleme karıştırması dışında tamamen deterministiktir.
    /// </summary>
    public static class TournamentScheduler
    {
        public const int MinScore = 0;
        public const int MaxScore = 50;

        /// <summary>Tur r'nin tarihi: StartDate + 7*(r-1) gün (saat StartDate'inki).</summary>
        public static DateTime RoundDate(DateTime startDate, int round) => startDate.AddDays(7 * (round - 1));

        /// <summary>Lig tur sayısı: çift n için n-1, tek n için n (her turda bir takım bay).</summary>
        public static int LeagueRoundCount(int teamCount) =>
            teamCount < 2 ? 0 : (teamCount % 2 == 0 ? teamCount - 1 : teamCount);

        /// <summary>n'den büyük/eşit en küçük 2'nin kuvveti (en az 2).</summary>
        public static int NextPowerOfTwo(int n)
        {
            var size = 2;
            while (size < n) size *= 2;
            return size;
        }

        public static int Log2(int powerOfTwo)
        {
            var r = 0;
            while ((1 << r) < powerOfTwo) r++;
            return r;
        }

        // ------------------------------------------------------------------ Lig

        /// <summary>
        /// Circle method ile tek devreli lig fikstürü. Takım sayısı tekse her turda bir takım bay
        /// geçer ve bunun için satır oluşturulmaz. Position turdaki sıradır (0'dan).
        /// </summary>
        public static List<TournamentFixture> League(IReadOnlyList<int> teamIds, DateTime startDate)
        {
            var slots = teamIds.Select(id => (int?)id).ToList();
            if (slots.Count % 2 == 1) slots.Add(null); // bay
            var m = slots.Count;
            var fixtures = new List<TournamentFixture>();
            if (m < 2) return fixtures;

            for (var r = 0; r < m - 1; r++)
            {
                var round = r + 1;
                var position = 0;
                for (var i = 0; i < m / 2; i++)
                {
                    var a = slots[i];
                    var b = slots[m - 1 - i];
                    if (a == null || b == null) continue; // bay: satır yok

                    // Sabit takım ev/deplasmanı turdan tura değiştirir
                    var swap = i == 0 && r % 2 == 1;
                    fixtures.Add(new TournamentFixture
                    {
                        Round = round,
                        Position = position++,
                        HomeTeamId = swap ? b : a,
                        AwayTeamId = swap ? a : b,
                        MatchDate = RoundDate(startDate, round),
                        Status = FixtureStatus.Scheduled,
                    });
                }

                // Döndürme: ilk eleman sabit, son eleman 1. indekse geçer
                var last = slots[m - 1];
                slots.RemoveAt(m - 1);
                slots.Insert(1, last);
            }
            return fixtures;
        }

        // ------------------------------------------------------------------ Eleme

        /// <summary>Fisher–Yates karıştırma (elemedeki tek rastgele adım).</summary>
        public static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// Eleme ağacı. teamIds ağaç sırasındadır (karıştırma çağıranda yapılır). Boyut bir sonraki
        /// 2'nin kuvvetine tamamlanır; eksik yerler bay olur (rakibi null, Status 2, takım otomatik
        /// üst tura geçer). Bütün turların satırları baştan oluşturulur.
        /// Baylar tur 1'de önce çift, sonra tek pozisyonlara dağıtılır; bay sayısı her zaman
        /// maç sayısından az olduğundan iki bay karşılaşmaz.
        /// </summary>
        public static List<TournamentFixture> Knockout(IReadOnlyList<int> teamIds, DateTime startDate)
        {
            var n = teamIds.Count;
            var size = NextPowerOfTwo(n);
            var rounds = Log2(size);
            var firstRoundMatches = size / 2;
            var byes = size - n;

            var byePositions = Enumerable.Range(0, firstRoundMatches)
                .OrderBy(p => p % 2).ThenBy(p => p)
                .Take(byes)
                .ToHashSet();

            var fixtures = new List<TournamentFixture>();
            var next = 0;
            for (var p = 0; p < firstRoundMatches; p++)
            {
                var isBye = byePositions.Contains(p);
                var home = next < n ? teamIds[next++] : (int?)null;
                var away = !isBye && next < n ? teamIds[next++] : (int?)null;
                fixtures.Add(new TournamentFixture
                {
                    Round = 1,
                    Position = p,
                    HomeTeamId = home,
                    AwayTeamId = away,
                    MatchDate = isBye ? null : RoundDate(startDate, 1),
                    Status = FixtureStatus.Scheduled,
                });
            }

            for (var r = 2; r <= rounds; r++)
            {
                for (var p = 0; p < size >> r; p++)
                {
                    fixtures.Add(new TournamentFixture
                    {
                        Round = r,
                        Position = p,
                        MatchDate = RoundDate(startDate, r),
                        Status = FixtureStatus.Scheduled,
                    });
                }
            }

            // Bayları otomatik ilerlet (zincirleme ilerleme Propagate içinde)
            foreach (var f in fixtures.Where(f => f.Round == 1).ToList())
            {
                if (f.HomeTeamId == null || f.AwayTeamId == null)
                {
                    MarkBye(fixtures, f);
                }
            }
            return fixtures;
        }

        public static int RoundCount(IEnumerable<TournamentFixture> fixtures) =>
            fixtures.Select(f => f.Round).DefaultIfEmpty(0).Max();

        public static TournamentFixture? Find(IEnumerable<TournamentFixture> fixtures, int round, int position) =>
            fixtures.FirstOrDefault(f => f.Round == round && f.Position == position);

        /// <summary>Tur r, pozisyon p'nin kazananının gideceği fikstür (finalde null).</summary>
        public static TournamentFixture? NextFixture(IEnumerable<TournamentFixture> fixtures, TournamentFixture f) =>
            Find(fixtures, f.Round + 1, f.Position / 2);

        private static bool IsDecided(TournamentFixture f) => f.Status != FixtureStatus.Scheduled;

        private static void MarkBye(List<TournamentFixture> fixtures, TournamentFixture f)
        {
            f.Status = FixtureStatus.Bye;
            f.HomeScore = null;
            f.AwayScore = null;
            f.MatchDate = null;
            f.WinnerTeamId = f.HomeTeamId ?? f.AwayTeamId;
            Propagate(fixtures, f);
        }

        /// <summary>
        /// f'nin kazananını üst tura yerleştirir (çift p → Home, tek p → Away). Üst tur maçının iki
        /// besleyicisi de sonuçlandıysa ve bir tarafı boşsa o maç da bay olur ve zincirleme ilerler.
        /// Oynanmış üst tur maçına dokunulmadığını çağıran (CanChangeWinner) garanti eder.
        /// </summary>
        public static void Propagate(List<TournamentFixture> fixtures, TournamentFixture f)
        {
            var next = NextFixture(fixtures, f);
            if (next == null) return;

            if (f.Position % 2 == 0) next.HomeTeamId = f.WinnerTeamId;
            else next.AwayTeamId = f.WinnerTeamId;

            if (next.Status == FixtureStatus.Played) return;

            var feederA = Find(fixtures, f.Round, next.Position * 2);
            var feederB = Find(fixtures, f.Round, next.Position * 2 + 1);
            var feedersDecided = (feederA == null || IsDecided(feederA)) && (feederB == null || IsDecided(feederB));
            if (feedersDecided && (next.HomeTeamId == null || next.AwayTeamId == null))
            {
                MarkBye(fixtures, next);
            }
            else if (next.Status == FixtureStatus.Bye)
            {
                // Önceden bay sayılmış ama artık iki takım da belli (yalnızca teorik durum)
                next.Status = FixtureStatus.Scheduled;
                next.WinnerTeamId = null;
                Propagate(fixtures, next);
            }
        }

        /// <summary>Kazanan değiştirilebilir mi: üst tur maçı oynanmadıysa evet.</summary>
        public static bool CanChangeWinner(IEnumerable<TournamentFixture> fixtures, TournamentFixture f)
        {
            var next = NextFixture(fixtures, f);
            return next == null || next.Status != FixtureStatus.Played;
        }

        // ------------------------------------------------------------------ Puan tablosu

        private static readonly StringComparer TeamNameComparer =
            StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: true);

        /// <summary>
        /// Galibiyet 3, beraberlik 1, mağlubiyet 0. Sıralama: puan, averaj, atılan gol, takım adı
        /// (son olarak TeamId). Yalnızca oynanmış (Status 1) fikstürler sayılır.
        /// </summary>
        public static List<StandingRowDto> Standings(IEnumerable<TeamRefDto> teams, IEnumerable<TournamentFixture> fixtures)
        {
            var rows = teams
                .GroupBy(t => t.TeamId).Select(g => g.First())
                .ToDictionary(t => t.TeamId, t => new StandingRowDto { Team = t });

            foreach (var f in fixtures.Where(f => f.Status == FixtureStatus.Played))
            {
                if (f.HomeTeamId is not int h || f.AwayTeamId is not int a) continue;
                if (f.HomeScore is not int hs || f.AwayScore is not int @as) continue;
                if (!rows.TryGetValue(h, out var home) || !rows.TryGetValue(a, out var away)) continue;

                Apply(home, hs, @as);
                Apply(away, @as, hs);
            }

            foreach (var r in rows.Values) r.GoalDiff = r.GoalsFor - r.GoalsAgainst;

            return rows.Values
                .OrderByDescending(r => r.Points)
                .ThenByDescending(r => r.GoalDiff)
                .ThenByDescending(r => r.GoalsFor)
                .ThenBy(r => r.Team.TeamName ?? string.Empty, TeamNameComparer)
                .ThenBy(r => r.Team.TeamId)
                .ToList();
        }

        private static void Apply(StandingRowDto row, int scored, int conceded)
        {
            row.Played++;
            row.GoalsFor += scored;
            row.GoalsAgainst += conceded;
            if (scored > conceded) { row.Won++; row.Points += 3; }
            else if (scored == conceded) { row.Drawn++; row.Points += 1; }
            else row.Lost++;
        }
    }
}
