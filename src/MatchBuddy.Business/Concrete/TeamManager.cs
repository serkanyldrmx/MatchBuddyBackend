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

        public IResult Delete(Team team)
        {
            //if (team.TeamName.Length < 3)
            //{
            //    return new ErrorResult(Messages.TeamNameInvalid);
            //}
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
