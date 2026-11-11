using MatchBuddy.Api.Model;
using MatchBuddy.Api.Security;
using MatchBuddy.Business.Abstract;
using MatchBuddy.Entities.DTOs;
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
        private readonly IChatService _chatService;

        public GroupsController(IGroupService groupService, IGroupPlayerService groupPlayerService, IChatService chatService)
        {
            _chatService = chatService;
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

        /// <summary>Grup sohbetlerim: üyeler (fotoğraflarıyla), son mesaj ve okunmamış sayısı; son mesaja göre sıralı.</summary>
        [HttpGet("MyGroups")]
        public ActionResult<List<GroupSummaryDto>> MyGroups() => _chatService.MyGroups(User.GetPlayerId(), User.IsAdmin());

        // Grubu kuran kişi her zaman üyedir; engelli / işletme / silinmiş oyuncular eklenmez
        [HttpPost("SaveGroup")]
        public IActionResult SaveGroup(GroupModel groupModel)
        {
            var result = _chatService.CreateGroup(User.GetPlayerId(), groupModel.GroupName, groupModel.PlayerList);
            return this.ToHttp(result, result.Success ? new { Success = true, GroupId = result.Data } : null);
        }

        /// <summary>Gruba üye ekle (her üye ekleyebilir).</summary>
        [HttpPost("AddMembers")]
        public IActionResult AddMembers(GroupMembersModel model, [FromServices] MatchBuddy.Api.Hubs.RealtimeNotifier realtime)
        {
            var result = _chatService.AddMembers(User.GetPlayerId(), model.GroupId, model.PlayerList);
            if (result.Success) _ = realtime.GroupChangedAsync(model.GroupId, Enumerable.Empty<int>());
            return this.ToHttp(result);
        }

        /// <summary>Gruptan çıkar (yalnızca kurucu ya da admin).</summary>
        [HttpPost("RemoveMember")]
        public IActionResult RemoveMember(GroupMembersModel model, [FromServices] MatchBuddy.Api.Hubs.RealtimeNotifier realtime)
        {
            var result = _chatService.RemoveMember(User.GetPlayerId(), model.GroupId, model.PlayerId, User.IsAdmin());
            if (result.Success) _ = realtime.GroupChangedAsync(model.GroupId, new[] { model.PlayerId });
            return this.ToHttp(result);
        }

        /// <summary>Gruptan ayrıl. Son üye ayrılırsa grup silinir.</summary>
        [HttpPost("Leave")]
        public IActionResult Leave(GroupMembersModel model, [FromServices] MatchBuddy.Api.Hubs.RealtimeNotifier realtime)
        {
            var result = _chatService.RemoveMember(User.GetPlayerId(), model.GroupId, User.GetPlayerId(), User.IsAdmin());
            if (result.Success) _ = realtime.GroupChangedAsync(model.GroupId, Enumerable.Empty<int>());
            return this.ToHttp(result);
        }

        /// <summary>Grup sohbetini okundu yap (açıldığında / yeni mesaj geldiğinde).</summary>
        [HttpPost("MarkRead")]
        public IActionResult MarkRead(GroupMembersModel model) =>
            this.ToHttp(_chatService.MarkGroupRead(User.GetPlayerId(), model.GroupId));

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
            return this.ToHttp(result);
        }
    }
}
