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

        // Stadyum ekleme: sadece admin
        [Authorize(Roles = Roles.Admin)]
        [HttpPost("SaveStadium")]
        public IActionResult SaveStadium(StadiumModel stadiumModel)
        {
            var stadium = new Stadium()
            {
                StadiumName = stadiumModel.StadiumName,
                City = stadiumModel.City,
                District = stadiumModel.District,
                Location = stadiumModel.Location,
                Address = stadiumModel.Address,
                OpeningTime = stadiumModel.OpeningTime,
                ClosingTime = stadiumModel.ClosingTime,
                Description = stadiumModel.Description,
                CreatedAt = DateTime.Now,
            };
            var result = _stadiumService.Add(stadium);
            if (result.Success)
            {
                return Ok(result);
            }
            return this.ToHttp(result);
        }

        [HttpGet("GetStadiumMatchList")]
        public List<GetStadiumMatchModel> GetStadiumMatchList()
        {
            var result = _stadiumService.GetStadiumMatch();
            return result.Data;
        }

        [HttpGet("GetStadiumList")]
        public List<Stadium> GetStadiumList()
        {
            var result = _stadiumService.GetAll();
            return result.Data;
        }
    }
}
