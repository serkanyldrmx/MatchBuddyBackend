using MatcBuddy.Business.Abstract;
using MatcBuddy.Business.Constants;
using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.DataAccess.Concrete.EntityFramework;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Business.Concrete
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

        public bool CanManage(int teamId, int playerId)
        {
            var creator = _teamDal.GetCreatorId(teamId);
            return creator.HasValue ? creator.Value == playerId : _teamDal.IsPlayerInTeam(teamId, playerId);
        }

        public IResult Leave(int teamId, int playerId)
        {
            if (!_teamDal.IsPlayerInTeam(teamId, playerId))
            {
                return new ErrorResult(ErrorCodes.Team.NotMember);
            }
            return _teamDal.Leave(teamId, playerId)
                ? new Result(true, Messages.Deleted)
                : new ErrorResult(ErrorCodes.Team.LastMember);
        }

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
