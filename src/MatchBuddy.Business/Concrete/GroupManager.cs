using MatchBuddy.Business.Abstract;
using MatchBuddy.Business.Constants;
using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Business.Concrete
{
    public class GroupManager :IGroupService
    {
        IGroupDal _groupDal;
        public GroupManager(IGroupDal groupDal)
        {
            _groupDal = groupDal;
        }

        public IResult Add(Group group)
        {
            //business codes
            if (group.GroupName.Length < 3)
            {
                return new ErrorResult(Messages.GroupNameInvalid);
            }
            _groupDal.Add(group);
            return new Result(true, Messages.Added);
        }

        public IDataResult<Group> GetGroupInfo(int groupId)
        {
            return new SuccessDataResult<Group>(_groupDal.Get(p => p.GroupId == groupId));
        }

        public IDataResult<List<Group>> GetGroups()
        {
            //iş Kodları
            //yetkisi varmı? 

            //if (DateTime.Now.Hour == 22)
            //{
            //    return new ErrorDataResult<List<Group>>(Messages.MaintenanceTime);
            //}
            return new SuccessDataResult<List<Group>>(_groupDal.GetAll(), Messages.GroupsListed);
        }

        public IResult Update(Group group)
        {
            if (group.GroupName.Length < 2)
            {
                return new ErrorResult(Messages.GroupNameInvalid);
            }
            _groupDal.Update(group);
            return new Result(true, Messages.Update);
        }
    }
}
