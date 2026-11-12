using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatcBuddy.Business.Abstract
{
    public interface IPlayerTeamService
    {
        IResult Add(PlayerTeam playerTeam);
    }
}
