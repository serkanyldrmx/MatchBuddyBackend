using MatchBuddy.Business.Abstract;
using MatchBuddy.Business.Constants;
using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.DataAccess.Concrete.EntityFramework;
using MatchBuddy.Entities.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace MatchBuddy.Business.Concrete
{
    public class PlayerTeamManager : IPlayerTeamService
    {
        IPlayerTeamDal _playerTeamDal;
        public PlayerTeamManager(IPlayerTeamDal playerTeamDal)
        {
            _playerTeamDal = playerTeamDal;
        }

        public IResult Add(PlayerTeam playerTeam)
        {
            _playerTeamDal.Add(playerTeam);
            return new Result(true, Messages.Added);
        }
    }
}
