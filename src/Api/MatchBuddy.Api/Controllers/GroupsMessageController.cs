using MatchBuddy.Api.Model;
using MatchBuddy.Business.Abstract;
using MatchBuddy.Business.Concrete;
using MatchBuddy.DataAccess.Concrete.EntityFramework;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;
using Microsoft.AspNetCore.Mvc;

namespace MatchBuddy.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GroupsMessageController : ControllerBase
    {
        //Loosely coupled
        //nameing convention 
        //IoC Container -- Inversion of Controler

        private readonly IGroupMessageService _groupMessageService;

        public GroupsMessageController(IGroupMessageService groupMessageService)
        {
            _groupMessageService = groupMessageService;
        }

        //[HttpGet("GetGroupList")] //alyans (isim) verdik
        //public List<Group> GetPlayerList()
        //{
        //    IGroupService groupService = new GroupManager(new EFGroupDal());
        //    var result = groupService.GetGroups();
        //    return result.Data;
        //}

        //[HttpGet("GetGroupById")] 
        //public Group GetGroupById([FromQuery]int groupId)
        //{
        //    IGroupService groupService = new GroupManager(new EFGroupDal());
        //    var result = groupService.GetGroupInfo(groupId);
        //    return result.Data;
        //}
        [HttpPost("SaveGroupMessage")]
        public IActionResult SaveGroupMessage(GroupMessageModel groupMessageModel)
        {
            var groupMessage = new GroupMessage()
            {
                GroupId = groupMessageModel.GroupId,
                MatchMessage = groupMessageModel.MatchMessage,
                SendingDate = groupMessageModel.SendingDate,
                SendPlayerId = groupMessageModel.SendPlayerId,
                Status = groupMessageModel.Status
            };

            var result = _groupMessageService.Add(groupMessage);

            if (result.Success)
            {         
                return Ok(result);
            }

            return BadRequest(result);
        }

        [HttpPost("GetGroupMessageById")]
        public List<GroupMessageModelDto> GetGroupMessageById(GroupMessageModel groupMessageModel)
        { 
            IGroupMessageService groupMessageService = new GroupMessageManager(new EFGroupMessageDal());
            var result = groupMessageService.GetGroupMessageById(groupMessageModel.GroupId);
            return result.Data;
        }

        //[HttpPost("UpdateGroup")]
        //public IActionResult UpdateGroup(GroupModel groupModel)
        //{
        //    var group = new Group()
        //    {
        //        GroupName = groupModel.GroupName,
        //    };
        //    var result = _groupService.Update(group);
        //    if (result.Success)
        //    {
        //        return Ok(result);
        //    } 
        //    return BadRequest(result);
        //}
    }
}
