using MatcBuddy.Business.Abstract;
using MatcBuddy.Business.Constants;
using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.DataAccess.Concrete.EntityFramework;
using MatcBuddy.Entities.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace MatcBuddy.Business.Concrete
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
