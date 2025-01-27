using MatchBuddy.Core.DataAccess;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.DataAccess.Abstract
{
    public interface IGroupMessageDal: IEntityRepository<GroupMessage>
    {
        List<GroupMessageModelDto> GetGroupMessageById(int groupId);
    }
}
