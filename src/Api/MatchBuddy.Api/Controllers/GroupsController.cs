using MatchBuddy.Api.Model;
using MatchBuddy.Api.Security;
using MatchBuddy.Business.Abstract;
using MatchBuddy.Entities.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatchBuddy.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class GroupsController : ControllerBase
    {
        private readonly IGroupService _groupService;
        private readonly IGroupPlayerService _groupPlayerService;

        public GroupsController(IGroupService groupService, IGroupPlayerService groupPlayerService)
        {
            _groupService = groupService;
            _groupPlayerService = groupPlayerService;
        }

        // Kullanıcı sadece üyesi olduğu grupları görür (admin hepsini)
        [HttpGet("GetGroupList")]
        public List<Group> GetGroupList()
        {
            var groups = _groupService.GetGroups().Data;
            if (User.IsAdmin())
            {
                return groups;
            }
            var myGroupIds = _groupPlayerService.GetGroupIdsForPlayer(User.GetPlayerId()).ToHashSet();
            return groups.Where(g => myGroupIds.Contains(g.GroupId)).ToList();
        }

        [HttpGet("GetGroupById")]
        public ActionResult<Group> GetGroupById([FromQuery] int groupId)
        {
            if (!User.IsAdmin() && !_groupPlayerService.IsPlayerInGroup(groupId, User.GetPlayerId()))
            {
                return Forbid();
            }
            return _groupService.GetGroupInfo(groupId).Data;
        }

        [HttpPost("SaveGroup")]
        public IActionResult SaveGroup(GroupModel groupModel)
        {
            var group = new Group()
            {
                GroupName = groupModel.GroupName,
            };

            var result = _groupService.Add(group);
            if (!result.Success)
            {
                return BadRequest(result);
            }

            // Grubu oluşturan kişi de her zaman grubun üyesidir
            var playerIds = (groupModel.PlayerList ?? new List<int>())
                .Append(User.GetPlayerId())
                .Distinct();

            foreach (var playerId in playerIds)
            {
                var result1 = _groupPlayerService.Add(new GroupPlayer
                {
                    PlayerId = playerId,
                    GroupId = group.GroupId
                });

                if (!result1.Success)
                {
                    return BadRequest(result1);
                }
            }

            return Ok(result);
        }

        [HttpPost("UpdateGroup")]
        public IActionResult UpdateGroup(GroupModel groupModel)
        {
            if (!User.IsAdmin() && !_groupPlayerService.IsPlayerInGroup(groupModel.GroupId, User.GetPlayerId()))
            {
                return Forbid();
            }

            var group = new Group()
            {
                GroupId = groupModel.GroupId,
                GroupName = groupModel.GroupName,
            };
            var result = _groupService.Update(group);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }
    }
}
