using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Business.Abstract
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
