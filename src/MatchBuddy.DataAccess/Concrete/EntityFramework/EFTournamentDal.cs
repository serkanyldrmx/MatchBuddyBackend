using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatchBuddy.DataAccess.Concrete.EntityFramework
{
    /// <summary>
    /// Turnuva iş kuralları (docs/tournaments-api.md). Her yazma işlemi tek context + transaction;
    /// turnuva satırı UPDLOCK ile kilitlenir. Fikstür/ağaç mantığı TournamentScheduler'da.
    /// </summary>
    public class EFTournamentDal : ITournamentDal
    {
        private const string TournamentNotFound = ErrorCodes.Tournament.NotFound;
        private const string FixtureNotFound = ErrorCodes.Tournament.FixtureNotFound;
        private const string TeamNotFound = ErrorCodes.Team.NotFound;
        private const int MembersPreview = 6;

        // ------------------------------------------------------------------ Okuma

        public List<TournamentSummaryDto> List(int me, byte? status)
        {
            using var context = new MatchBuddyContext();
            var query = context.Tournaments.AsNoTracking();
            if (status.HasValue) query = query.Where(t => t.Status == status.Value);

            // Önce devam eden, sonra kayıt açık, sonra tamamlanan; her grupta StartDate'e göre
            var tournaments = query
                .OrderBy(t => t.Status == TournamentStatus.Ongoing ? 0 : t.Status == TournamentStatus.Registration ? 1 : 2)
                .ThenBy(t => t.StartDate)
                .ThenBy(t => t.TournamentId)
                .ToList();

            return BuildSummaries(context, me, tournaments, (t, _) => new TournamentSummaryDto());
        }

        public OperationResult<TournamentDetailDto> Detail(int me, bool isAdmin, int tournamentId)
        {
            using var context = new MatchBuddyContext();
            var t = context.Tournaments.AsNoTracking().FirstOrDefault(x => x.TournamentId == tournamentId);
            if (t == null) return OperationResult<TournamentDetailDto>.NotFound(TournamentNotFound);

            var dto = BuildSummaries(context, me, new List<Tournament> { t }, (_, _) => new TournamentDetailDto())
                .Cast<TournamentDetailDto>().Single();

            dto.Description = t.Description;
            dto.CanManage = CanManage(t, me, isAdmin);

            var registrations = context.TournamentTeams.AsNoTracking()
                .Where(x => x.TournamentId == tournamentId)
                .OrderBy(x => x.JoinedAt).ThenBy(x => x.TournamentTeamId)
                .Select(x => x.TeamId)
                .ToList();

            var fixtures = context.TournamentFixtures.AsNoTracking()
                .Where(f => f.TournamentId == tournamentId)
                .OrderBy(f => f.Round).ThenBy(f => f.Position)
                .ToList();

            var teamIds = registrations
                .Concat(fixtures.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId, f.WinnerTeamId })
                    .Where(id => id.HasValue).Select(id => id!.Value))
                .Distinct().ToList();
            var teamNames = context.Teams.AsNoTracking()
                .Where(x => teamIds.Contains(x.TeamId))
                .Select(x => new { x.TeamId, x.TeamName })
                .ToList()
                .ToDictionary(x => x.TeamId, x => x.TeamName);
            TeamRefDto? Ref(int? id) => id is int v
                ? new TeamRefDto { TeamId = v, TeamName = teamNames.TryGetValue(v, out var n) ? n : null }
                : null;

            var members = (from pt in context.PlayerTeam.AsNoTracking()
                           join p in context.Players.AsNoTracking() on pt.PlayerId equals p.PlayerId
                           where registrations.Contains(pt.TeamId) && !p.IsDeleted
                           orderby pt.PlayerTeamId
                           select new { pt.TeamId, Player = p })
                          .ToList()
                          .GroupBy(x => x.TeamId)
                          .ToDictionary(g => g.Key, g => g.Select(x => x.Player).DistinctBy(p => p.PlayerId).ToList());

            var myTeams = MyTeamIds(context, me);

            dto.Teams = registrations.Select(id =>
            {
                members.TryGetValue(id, out var list);
                list ??= new List<Player>();
                return new TournamentTeamDto
                {
                    TeamId = id,
                    TeamName = teamNames.TryGetValue(id, out var n) ? n : null,
                    MemberCount = list.Count,
                    Members = list.Take(MembersPreview).Select(PlayerSummaryDto.FromPlayer).ToList(),
                    IsMine = myTeams.Contains(id),
                };
            }).ToList();

            dto.Fixtures = fixtures.Select(f => new TournamentFixtureDto
            {
                FixtureId = f.FixtureId,
                Round = f.Round,
                Position = f.Position,
                HomeTeam = Ref(f.HomeTeamId),
                AwayTeam = Ref(f.AwayTeamId),
                MatchDate = f.MatchDate,
                HomeScore = f.HomeScore,
                AwayScore = f.AwayScore,
                WinnerTeamId = f.WinnerTeamId,
                Status = f.Status,
            }).ToList();

            dto.Standings = t.Format == TournamentFormat.League
                ? TournamentScheduler.Standings(registrations.Select(id => Ref(id)!), fixtures)
                : new List<StandingRowDto>();
            dto.RoundCount = TournamentScheduler.RoundCount(fixtures);

            if (t.Status == TournamentStatus.Registration)
            {
                var registered = registrations.ToHashSet();
                dto.MyEligibleTeams = context.Teams.AsNoTracking()
                    .Where(x => myTeams.Contains(x.TeamId))
                    .OrderBy(x => x.TeamName).ThenBy(x => x.TeamId)
                    .Select(x => new TeamRefDto { TeamId = x.TeamId, TeamName = x.TeamName })
                    .ToList()
                    .Where(x => !registered.Contains(x.TeamId))
                    .ToList();
            }

            return OperationResult<TournamentDetailDto>.Ok(dto);
        }

        private static List<TournamentSummaryDto> BuildSummaries(MatchBuddyContext context, int me,
            List<Tournament> tournaments, Func<Tournament, int, TournamentSummaryDto> factory)
        {
            if (tournaments.Count == 0) return new List<TournamentSummaryDto>();
            var ids = tournaments.Select(t => t.TournamentId).ToList();

            var registrations = context.TournamentTeams.AsNoTracking()
                .Where(x => ids.Contains(x.TournamentId))
                .Select(x => new { x.TournamentId, x.TeamId })
                .ToList();

            var stadiumIds = tournaments.Where(t => t.StadiumId.HasValue).Select(t => t.StadiumId!.Value).Distinct().ToList();
            var stadiums = context.Stadiums.AsNoTracking()
                .Where(s => stadiumIds.Contains(s.StadiumId))
                .Select(s => new { s.StadiumId, s.StadiumName })
                .ToList()
                .ToDictionary(s => s.StadiumId, s => s.StadiumName);

            var creatorIds = tournaments.Select(t => t.CreatedByPlayerId).Distinct().ToList();
            var creators = context.Players.AsNoTracking()
                .Where(p => creatorIds.Contains(p.PlayerId))
                .ToList()
                .ToDictionary(p => p.PlayerId);

            var winnerIds = tournaments.Where(t => t.WinnerTeamId.HasValue).Select(t => t.WinnerTeamId!.Value).Distinct().ToList();
            var winners = context.Teams.AsNoTracking()
                .Where(x => winnerIds.Contains(x.TeamId))
                .Select(x => new { x.TeamId, x.TeamName })
                .ToList()
                .ToDictionary(x => x.TeamId, x => x.TeamName);

            var myTeams = MyTeamIds(context, me);

            return tournaments.Select(t =>
            {
                var dto = factory(t, me);
                var teams = registrations.Where(r => r.TournamentId == t.TournamentId).Select(r => r.TeamId).ToList();
                dto.TournamentId = t.TournamentId;
                dto.Name = t.Name;
                dto.Format = t.Format;
                dto.Status = t.Status;
                dto.StartDate = t.StartDate;
                dto.MaxTeams = t.MaxTeams;
                dto.TeamCount = teams.Count;
                dto.Stadium = t.StadiumId is int sid && stadiums.TryGetValue(sid, out var sn)
                    ? new TournamentStadiumDto { StadiumId = sid, StadiumName = sn }
                    : null;
                dto.CreatedBy = creators.TryGetValue(t.CreatedByPlayerId, out var c) ? PlayerSummaryDto.FromPlayer(c) : null;
                dto.Winner = t.WinnerTeamId is int wid
                    ? new TeamRefDto { TeamId = wid, TeamName = winners.TryGetValue(wid, out var wn) ? wn : null }
                    : null;
                dto.MyTeamIds = teams.Where(myTeams.Contains).ToList();
                return dto;
            }).ToList();
        }

        private static HashSet<int> MyTeamIds(MatchBuddyContext context, int me) =>
            context.PlayerTeam.AsNoTracking().Where(pt => pt.PlayerId == me).Select(pt => pt.TeamId).ToHashSet();

        private static bool CanManage(Tournament t, int me, bool isAdmin) => isAdmin || t.CreatedByPlayerId == me;

        // ------------------------------------------------------------------ Yazma

        private static Tournament? LockTournament(MatchBuddyContext context, int tournamentId) =>
            context.Tournaments
                .FromSqlInterpolated($"SELECT * FROM Tournaments WITH (UPDLOCK, HOLDLOCK) WHERE TournamentId = {tournamentId}")
                .AsEnumerable()
                .FirstOrDefault();

        public OperationResult<int> Create(int me, TournamentCreateInput input)
        {
            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            if (input.StadiumId.HasValue && !context.Stadiums.Any(s => s.StadiumId == input.StadiumId.Value))
                return OperationResult<int>.NotFound(ErrorCodes.Tournament.StadiumNotFound);

            var tournament = new Tournament
            {
                Name = input.Name!,
                Description = input.Description,
                Format = input.Format,
                Status = TournamentStatus.Registration,
                StartDate = input.StartDate!.Value,
                MaxTeams = input.MaxTeams,
                StadiumId = input.StadiumId,
                CreatedByPlayerId = me,
                CreatedAt = DateTime.Now,
            };
            context.Tournaments.Add(tournament);
            context.SaveChanges();
            transaction.Commit();
            return OperationResult<int>.Ok(tournament.TournamentId, Msg.Get(MessageKeys.Tournament.Created));
        }

        public OperationResult<bool> RegisterTeam(int me, int tournamentId, int teamId)
        {
            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var t = LockTournament(context, tournamentId);
            if (t == null) return OperationResult<bool>.NotFound(TournamentNotFound);
            if (!context.Teams.Any(x => x.TeamId == teamId)) return OperationResult<bool>.NotFound(TeamNotFound);
            if (!context.PlayerTeam.Any(pt => pt.TeamId == teamId && pt.PlayerId == me))
                return OperationResult<bool>.Forbidden(ErrorCodes.Tournament.RegisterNotTeamMember);
            if (t.Status != TournamentStatus.Registration)
                return OperationResult<bool>.BadRequest(ErrorCodes.Tournament.RegistrationClosed);

            var registered = context.TournamentTeams.Where(x => x.TournamentId == tournamentId).Select(x => x.TeamId).ToList();
            if (registered.Contains(teamId))
                return OperationResult<bool>.BadRequest(ErrorCodes.Tournament.TeamAlreadyRegistered);
            if (registered.Count >= t.MaxTeams)
                return OperationResult<bool>.BadRequest(ErrorCodes.Tournament.Full);

            context.TournamentTeams.Add(new TournamentTeam { TournamentId = tournamentId, TeamId = teamId, JoinedAt = DateTime.Now });
            context.SaveChanges();
            transaction.Commit();
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Tournament.TeamRegistered));
        }

        public OperationResult<bool> WithdrawTeam(int me, bool isAdmin, int tournamentId, int teamId)
        {
            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var t = LockTournament(context, tournamentId);
            if (t == null) return OperationResult<bool>.NotFound(TournamentNotFound);
            var row = context.TournamentTeams.FirstOrDefault(x => x.TournamentId == tournamentId && x.TeamId == teamId);
            if (row == null) return OperationResult<bool>.NotFound(ErrorCodes.Tournament.TeamNotRegistered);
            if (!CanManage(t, me, isAdmin) && !context.PlayerTeam.Any(pt => pt.TeamId == teamId && pt.PlayerId == me))
                return OperationResult<bool>.Forbidden(ErrorCodes.Tournament.WithdrawForbidden);
            if (t.Status != TournamentStatus.Registration)
                return OperationResult<bool>.BadRequest(ErrorCodes.Tournament.WithdrawAfterStart);

            context.TournamentTeams.Remove(row);
            context.SaveChanges();
            transaction.Commit();
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Tournament.TeamWithdrawn));
        }

        public OperationResult<bool> Start(int me, bool isAdmin, int tournamentId)
        {
            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var t = LockTournament(context, tournamentId);
            if (t == null) return OperationResult<bool>.NotFound(TournamentNotFound);
            if (!CanManage(t, me, isAdmin))
                return OperationResult<bool>.Forbidden(ErrorCodes.Tournament.StartForbidden);
            if (t.Status != TournamentStatus.Registration)
                return OperationResult<bool>.BadRequest(ErrorCodes.Tournament.AlreadyStarted);

            var teamIds = context.TournamentTeams
                .Where(x => x.TournamentId == tournamentId)
                .OrderBy(x => x.JoinedAt).ThenBy(x => x.TournamentTeamId)
                .Select(x => x.TeamId)
                .ToList();

            List<TournamentFixture> fixtures;
            if (t.Format == TournamentFormat.League)
            {
                var minLeague = P.Int(ParameterKeys.Tournament.LeagueMinTeamsToStart);
                if (teamIds.Count < minLeague) return OperationResult<bool>.BadRequest(ErrorCodes.Tournament.LeagueMinTeams, minLeague);
                fixtures = TournamentScheduler.League(teamIds, t.StartDate);
            }
            else
            {
                var minKnockout = P.Int(ParameterKeys.Tournament.KnockoutMinTeamsToStart);
                if (teamIds.Count < minKnockout) return OperationResult<bool>.BadRequest(ErrorCodes.Tournament.KnockoutMinTeams, minKnockout);
                TournamentScheduler.Shuffle(teamIds, Random.Shared);
                fixtures = TournamentScheduler.Knockout(teamIds, t.StartDate);
            }

            foreach (var f in fixtures) f.TournamentId = tournamentId;
            context.TournamentFixtures.AddRange(fixtures);
            t.Status = TournamentStatus.Ongoing;

            NotificationWriter.Add(context, NotificationType.TournamentUpdate,
                Msg.GetDefault(MessageKeys.Tournament.NotifStarted, t.Name, t.StartDate),
                null, null, MembersOf(context, teamIds));

            context.SaveChanges();
            transaction.Commit();
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Tournament.Started));
        }

        public OperationResult<bool> SubmitResult(int me, bool isAdmin, int fixtureId, int homeScore, int awayScore, int? winnerTeamId)
        {
            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var tournamentId = context.TournamentFixtures.Where(f => f.FixtureId == fixtureId)
                .Select(f => (int?)f.TournamentId).FirstOrDefault();
            if (tournamentId == null) return OperationResult<bool>.NotFound(FixtureNotFound);
            var t = LockTournament(context, tournamentId.Value);
            if (t == null) return OperationResult<bool>.NotFound(TournamentNotFound);
            if (!CanManage(t, me, isAdmin))
                return OperationResult<bool>.Forbidden(ErrorCodes.Tournament.ResultForbidden);
            if (t.Status != TournamentStatus.Ongoing)
                return OperationResult<bool>.BadRequest(ErrorCodes.Tournament.ResultNotOngoing);

            var fixtures = context.TournamentFixtures.Where(f => f.TournamentId == t.TournamentId).ToList();
            var fixture = fixtures.Single(f => f.FixtureId == fixtureId);
            if (fixture.Status == FixtureStatus.Bye || fixture.HomeTeamId is not int home || fixture.AwayTeamId is not int away)
                return OperationResult<bool>.BadRequest(ErrorCodes.Tournament.FixtureTeamsUndetermined);

            int? winner = homeScore > awayScore ? home : awayScore > homeScore ? away : null;
            var knockout = t.Format == TournamentFormat.Knockout;
            if (knockout && winner == null)
            {
                if (winnerTeamId == null)
                    return OperationResult<bool>.BadRequest(ErrorCodes.Tournament.DrawWinnerRequired);
                if (winnerTeamId != home && winnerTeamId != away)
                    return OperationResult<bool>.BadRequest(ErrorCodes.Tournament.WinnerNotInFixture);
                winner = winnerTeamId;
            }

            if (knockout && fixture.Status == FixtureStatus.Played && fixture.WinnerTeamId != winner
                && !TournamentScheduler.CanChangeWinner(fixtures, fixture))
                return OperationResult<bool>.BadRequest(ErrorCodes.Tournament.WinnerLocked);

            fixture.HomeScore = homeScore;
            fixture.AwayScore = awayScore;
            fixture.WinnerTeamId = winner;
            fixture.Status = FixtureStatus.Played;
            if (knockout) TournamentScheduler.Propagate(fixtures, fixture);

            var teamNames = context.Teams.AsNoTracking()
                .Where(x => x.TeamId == home || x.TeamId == away)
                .ToDictionary(x => x.TeamId, x => x.TeamName);
            NotificationWriter.Add(context, NotificationType.TournamentUpdate,
                Msg.GetDefault(MessageKeys.Tournament.NotifResult, t.Name, teamNames.GetValueOrDefault(home), homeScore, awayScore, teamNames.GetValueOrDefault(away)),
                null, null, MembersOf(context, new[] { home, away }));

            // Bitiş kontrolü
            int? champion = null;
            if (knockout)
            {
                var finalRound = TournamentScheduler.RoundCount(fixtures);
                if (fixture.Round == finalRound) champion = winner;
            }
            else if (fixtures.All(f => f.Status == FixtureStatus.Played))
            {
                var registered = context.TournamentTeams.Where(x => x.TournamentId == t.TournamentId).Select(x => x.TeamId).ToList();
                var names = context.Teams.AsNoTracking().Where(x => registered.Contains(x.TeamId))
                    .Select(x => new TeamRefDto { TeamId = x.TeamId, TeamName = x.TeamName }).ToList();
                champion = TournamentScheduler.Standings(names, fixtures).FirstOrDefault()?.Team.TeamId;
            }

            var message = Msg.Get(MessageKeys.Tournament.ResultSaved);
            if (champion is int championId)
            {
                t.Status = TournamentStatus.Finished;
                t.WinnerTeamId = championId;

                var winnerMembers = (from pt in context.PlayerTeam
                                     join p in context.Players on pt.PlayerId equals p.PlayerId
                                     where pt.TeamId == championId && !p.IsDeleted
                                     select pt.PlayerId).Distinct().ToList();
                foreach (var playerId in winnerMembers)
                {
                    ParticipationCore.AddActivity(context, ActivityType.TournamentWon, playerId,
                        teamId: championId, value: t.TournamentId);
                }

                var allTeams = context.TournamentTeams.Where(x => x.TournamentId == t.TournamentId).Select(x => x.TeamId).ToList();
                var championName = context.Teams.AsNoTracking().Where(x => x.TeamId == championId).Select(x => x.TeamName).FirstOrDefault();
                NotificationWriter.Add(context, NotificationType.TournamentUpdate,
                    Msg.GetDefault(MessageKeys.Tournament.NotifFinished, t.Name, championName),
                    null, null, MembersOf(context, allTeams));
                message = Msg.Get(MessageKeys.Tournament.ResultSavedFinished, championName);
            }

            context.SaveChanges();
            transaction.Commit();
            return OperationResult<bool>.Ok(true, message);
        }

        public OperationResult<bool> UpdateFixtureDate(int me, bool isAdmin, int fixtureId, DateTime matchDate)
        {
            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var fixture = context.TournamentFixtures.FirstOrDefault(f => f.FixtureId == fixtureId);
            if (fixture == null) return OperationResult<bool>.NotFound(FixtureNotFound);
            var t = LockTournament(context, fixture.TournamentId);
            if (t == null) return OperationResult<bool>.NotFound(TournamentNotFound);
            if (!CanManage(t, me, isAdmin))
                return OperationResult<bool>.Forbidden(ErrorCodes.Tournament.FixtureDateForbidden);
            if (fixture.Status == FixtureStatus.Bye)
                return OperationResult<bool>.BadRequest(ErrorCodes.Tournament.ByeHasNoDate);

            fixture.MatchDate = matchDate;
            context.SaveChanges();
            transaction.Commit();
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Tournament.FixtureDateUpdated));
        }

        public OperationResult<bool> Delete(int me, bool isAdmin, int tournamentId)
        {
            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var t = LockTournament(context, tournamentId);
            if (t == null) return OperationResult<bool>.NotFound(TournamentNotFound);
            if (!CanManage(t, me, isAdmin))
                return OperationResult<bool>.Forbidden(ErrorCodes.Tournament.DeleteForbidden);
            if (t.Status != TournamentStatus.Registration)
                return OperationResult<bool>.BadRequest(ErrorCodes.Tournament.DeleteOnlyRegistration);

            context.Tournaments.Remove(t); // TournamentTeams / Fixtures cascade
            context.SaveChanges();
            transaction.Commit();
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Tournament.Deleted));
        }

        private static List<int> MembersOf(MatchBuddyContext context, IEnumerable<int> teamIds)
        {
            var ids = teamIds.Distinct().ToList();
            return context.PlayerTeam.AsNoTracking()
                .Where(pt => ids.Contains(pt.TeamId))
                .Select(pt => pt.PlayerId)
                .Distinct()
                .ToList();
        }
    }
}
