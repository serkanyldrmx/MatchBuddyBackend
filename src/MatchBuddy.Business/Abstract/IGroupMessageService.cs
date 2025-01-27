using MatchBuddy.Api.Model;
using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Business.Abstract
{
    public interface IGroupMessageService
    {
        IDataResult<List<GroupMessage>> GetRecipientMessage(int playerId);
        //Gelen mesajları getir
        IDataResult<List<GroupMessage>> GetSendMessage(int playerId);
        //Gönderilen mesajları getir  
        //IDataResult<List<GroupMessage>> GetMessageById(GetMessageModel messageModel);
        IDataResult<List<GroupMessageModelDto>> GetGroupMessageById(int groupId);
        IResult Add(GroupMessage groupMessage);
    }
}
