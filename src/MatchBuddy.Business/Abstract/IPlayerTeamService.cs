using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.Entities.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatchBuddy.Business.Abstract
{
    public interface IPlayerTeamService
    {
        IResult Add(PlayerTeam playerTeam);
    }
}
