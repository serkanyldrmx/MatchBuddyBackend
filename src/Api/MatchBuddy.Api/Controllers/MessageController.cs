using MatchBuddy.Api.Model;
using MatchBuddy.Business.Abstract;
using MatchBuddy.Business.Concrete;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.DataAccess.Concrete.EntityFramework;
using MatchBuddy.Entities.Entity;
using Microsoft.AspNetCore.Mvc;
using System.Numerics;

namespace MatchBuddy.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MessageController : ControllerBase
    {
        IMessageService _messageService;
        public MessageController(IMessageService messageService)
        {
            _messageService = messageService;
        }

        [HttpGet("GetSendMessages")]
        public List<Message> GetSendMessages([FromQuery]int playerId)
        {
            var result = _messageService.GetSendMessage(playerId);
            return result.Data;
        }

        [HttpGet("GetRecipientMessages")]
        public List<Message> GetRecipientMessage([FromQuery]int playerId)
        {
            var result = _messageService.GetRecipientMessage(playerId);
            return result.Data;
        }

        [HttpPost("GetMessageById")]
        public List<Message> GetMessageById(MessageModel messageModel)
        {
            var MessageModel = new Entities.DTOs.GetMessageModel()
            {
                SendPlayerId = messageModel.SendPlayerId,
                RecipientPlayerId = messageModel.RecipientPlayerId
            };
            var result = _messageService.GetMessageById(MessageModel);
            return result.Data;
        }

        [HttpPost("MessageSave")]
        public IActionResult MessageSave(MessageSaveModel mesageModel)
        {
            var message = new Message()
            {
                SendPlayerId=mesageModel.SendPlayerId,
                RecipientPlayerId=mesageModel.RecipientPlayerId,
                MatchMessage = mesageModel.MatchMessage,
                SendingDate=mesageModel.SendingDate,
                Status = mesageModel.Status,    
            };
            var result = _messageService.Add(message);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }
    }
}
