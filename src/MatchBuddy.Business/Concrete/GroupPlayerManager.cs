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
    public class GroupPlayerManager : IGroupPlayerService
    {
        IGroupPlayerDal _groupPlayerDal;
        public GroupPlayerManager(IGroupPlayerDal groupPlayerDal)
        {
            _groupPlayerDal = groupPlayerDal;
        }

        public IResult Add(GroupPlayer groupPlayer)
        {
            _groupPlayerDal.Add(groupPlayer);
            return new Result(true, Messages.Added);
        }
    }
}
