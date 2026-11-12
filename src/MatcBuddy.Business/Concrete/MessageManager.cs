using MatcBuddy.Business.Abstract;
using MatcBuddy.Business.Constants;
using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.DataAccess.Concrete.EntityFramework;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Business.Concrete
{
    public class MessageManager: IMessageService
    {
        IMessageDal _messageDal;

        public MessageManager(IMessageDal messageDal)
        {
            _messageDal = messageDal;
        }

        public IDataResult<List<Message>> GetRecipientMessage(int playerId)
        {
            return new SuccessDataResult<List<Message>>(_messageDal.GetAll(x=>x.RecipientPlayerId == playerId));
        }

        public IDataResult<List<Message>> GetSendMessage(int playerId)
        {
            return new SuccessDataResult<List<Message>>(_messageDal.GetAll(x => x.SendPlayerId == playerId));
        }

        public IDataResult<List<Message>> GetMessageById(GetMessageModel messageModel)
        {
            return new SuccessDataResult<List<Message>>(_messageDal.GetAll(p => p.SendPlayerId == messageModel.SendPlayerId && p.RecipientPlayerId == messageModel.RecipientPlayerId ||
            p.SendPlayerId == messageModel.RecipientPlayerId && p.RecipientPlayerId == messageModel.SendPlayerId));
        }

        public IResult Add(Message message)
        {
            //business codes
            _messageDal.Add(message);
            return new Result(true, Messages.Added);
        }
    }
}
