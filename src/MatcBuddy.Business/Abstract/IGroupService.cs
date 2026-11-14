using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Business.Abstract
{
    public interface IGroupService
    {
        IDataResult<Group> GetGroupInfo(int groupId);
        //grup bilgilerini getir
        IDataResult<List<Group>> GetGroups();
        //Grupları Listele
        IResult Update(Group group);
        //grupu güncelle
        IResult Add(Group group);
        //Grup ekleme
    }
}
