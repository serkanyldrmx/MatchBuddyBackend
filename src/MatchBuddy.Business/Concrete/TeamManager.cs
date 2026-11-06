using MatchBuddy.Business.Abstract;
using MatchBuddy.Business.Constants;
using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.DataAccess.Concrete.EntityFramework;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Business.Concrete
{
    public class TeamManager : ITeamService
    {
        ITeamDal _teamDal;
        public TeamManager(ITeamDal teamDal)
        {
            _teamDal = teamDal;
        }

        public IResult Add(Team team)
        {
            if (team.TeamName.Length < 3)
            {
                return new ErrorResult(Messages.TeamNameInvalid);
            }
            _teamDal.Add(team);
            return new Result(true, Messages.Added);
        }

        public IResult Add(Team team, int creatorPlayerId)
        {
            if (team.TeamName == null || team.TeamName.Length < 3)
            {
                return new ErrorResult(Messages.TeamNameInvalid);
            }
            _teamDal.AddWithActivity(team, creatorPlayerId);
            return new Result(true, Messages.Added);
        }

        public IResult AddPlayerById(List<int> playerIds, int teamId)
        {
            _teamDal.SaveTeamByPlayerId(playerIds,teamId);
            return new Result(true, Messages.Added);
        }

        public bool IsPlayerInTeam(int teamId, int playerId) => _teamDal.IsPlayerInTeam(teamId, playerId);

        public IResult Delete(Team team)
        {
            //if (team.TeamName.Length < 3)
            //{
            //    return new ErrorResult(Messages.TeamNameInvalid);
            //}
            if (_teamDal.IsInAnyTournament(team.TeamId))
            {
                return new ErrorResult(Messages.TeamInTournament);
            }
            _teamDal.ClearMatchReferences(team.TeamId);
            _teamDal.Delete(team);
            return new Result(true, Messages.Deleted);
        }

        public IDataResult<List<Team>> GetAll()
        {
            return new SuccessDataResult<List<Team>>(_teamDal.GetAll(), Messages.PlayersListed);
        }

        public IResult Update(Team team)
        {
            //if (team.TeamName.Length < 3)
            //{
            //    return new ErrorResult(Messages.TeamNameInvalid);
            //}
            _teamDal.Update(team);
            return new Result(true, Messages.Update);
        }

        public IDataResult<List<GetTeamAndPlayer>> GetTeamAndPlayer()
        {
            return new SuccessDataResult<List<GetTeamAndPlayer>>(_teamDal.GetTeamAndPlayer());
        }
    }
}
