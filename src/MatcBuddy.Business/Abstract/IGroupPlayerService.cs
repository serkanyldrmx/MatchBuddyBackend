using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatcBuddy.Business.Abstract
{
    public interface IGroupPlayerService
    {
        IResult Add(GroupPlayer groupPlayer);
        bool IsPlayerInGroup(int groupId, int playerId);
        List<int> GetGroupIdsForPlayer(int playerId);
    }
}
