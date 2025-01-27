using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Business.Abstract
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
