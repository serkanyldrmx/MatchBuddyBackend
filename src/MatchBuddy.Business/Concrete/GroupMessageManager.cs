using MatchBuddy.Api.Model;
using MatchBuddy.Business.Abstract;
using MatchBuddy.Business.Constants;
using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.DataAccess.Concrete.EntityFramework;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Business.Concrete
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
