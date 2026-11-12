using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Business.Abstract
{
    public interface IMessageService
    {
        IDataResult<List<Message>> GetRecipientMessage(int playerId);
        //Gelen mesajları getir
        IDataResult<List<Message>> GetSendMessage(int playerId);
        //Gönderilen mesajları getir  
        IDataResult<List<Message>> GetMessageById(GetMessageModel messageModel);
        IResult Add(Message message);
    }
}
