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
    public class GroupsController : ControllerBase
    {
        //Loosely coupled
        //nameing convention 
        //IoC Container -- Inversion of Controler

        private readonly IGroupService _groupService;
        private readonly IGroupPlayerService _groupPlayerService;

        public GroupsController(IGroupService groupService, IGroupPlayerService groupPlayerService)
        {
            _groupService = groupService;
            _groupPlayerService = groupPlayerService;
        }

        [HttpGet("GetGroupList")] //alyans (isim) verdik
        public List<Group> GetPlayerList()
        {
            IGroupService groupService = new GroupManager(new EFGroupDal());
            var result = groupService.GetGroups();
            return result.Data;
        }

        [HttpGet("GetGroupById")] 
        public Group GetGroupById([FromQuery]int groupId)
        {
            IGroupService groupService = new GroupManager(new EFGroupDal());
            var result = groupService.GetGroupInfo(groupId);
            return result.Data;
        }
        [HttpPost("SaveGroup")]
        public IActionResult SaveGroup(GroupModel groupModel)
        {
            var group = new Group()
            {
                GroupName = groupModel.GroupName,
            };

            var result = _groupService.Add(group);

            if (result.Success)
            {
                foreach (var playerId in groupModel.PlayerList)
                {
                    var groupPlayer = new GroupPlayer()
                    {
                        PlayerId = playerId,
                        GroupId = group.GroupId // group nesnesinin Id'sini kullanarak gruba ait olduğu belirtiliyor
                    };

                    var result1 = _groupPlayerService.Add(groupPlayer);

                    if (!result1.Success)
                    {
                        return BadRequest(result1); // Eğer ekleme işlemi başarısız olursa, döngüyü sonlandırıp hata döndürüyoruz
                    }
                }

                return Ok(result);
            }

            return BadRequest(result);
        }

        [HttpPost("UpdateGroup")]
        public IActionResult UpdateGroup(GroupModel groupModel)
        {
            var group = new Group()
            {
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
