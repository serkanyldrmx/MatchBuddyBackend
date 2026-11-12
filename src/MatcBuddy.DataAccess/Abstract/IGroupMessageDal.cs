using MatcBuddy.Core.DataAccess;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.DataAccess.Abstract
{
    public interface IGroupMessageDal: IEntityRepository<GroupMessage>
    {
        List<GroupMessageModelDto> GetGroupMessageById(int groupId);
    }
}
