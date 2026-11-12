using MatcBuddy.Api.Model;
using MatcBuddy.Business.Abstract;
using MatcBuddy.Business.Constants;
using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.DataAccess.Concrete.EntityFramework;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Business.Concrete
{
    public class GroupMessageManager: IGroupMessageService
    {
        IGroupMessageDal _groupMessageDal;

        public GroupMessageManager(IGroupMessageDal groupMessageDal)
        {
            _groupMessageDal = groupMessageDal;

        }

        public IDataResult<List<GroupMessage>> GetRecipientMessage(int playerId)
        {
            return new SuccessDataResult<List<GroupMessage>>(_groupMessageDal.GetAll(x=>x.SendPlayerId == playerId));
            //burada bir hata olabilir
        }

        public IDataResult<List<GroupMessage>> GetSendMessage(int playerId)
        {
            return new SuccessDataResult<List<GroupMessage>>(_groupMessageDal.GetAll(x => x.SendPlayerId == playerId));
        }

        public IDataResult<List<GroupMessageModelDto>> GetGroupMessageById(int groupId)
        {
            return new SuccessDataResult<List<GroupMessageModelDto>>(_groupMessageDal.GetGroupMessageById(groupId));
        }

        public IResult Add(GroupMessage groupMessage)
        {
            //business codes
            _groupMessageDal.Add(groupMessage);
            return new Result(true, Messages.Added);
        }
    }
}
