using MatcBuddy.Api.Model;
using MatcBuddy.Business.Abstract;
using MatcBuddy.Api.Security;
using Microsoft.AspNetCore.Authorization;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;
using Microsoft.AspNetCore.Mvc;

namespace MatcBuddy.Api.Controllers
{


    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class StadiumController : ControllerBase
    {
        IStadiumService _stadiumService;

        public StadiumController(IStadiumService stadiumService)
        {
            _stadiumService = stadiumService;
        }

        [HttpGet("GetStadiumList")]
        public List<Stadium> GetStadiumList()
        {
            var result = _stadiumService.GetAll();
            return result.Data;
        }
    }
}
