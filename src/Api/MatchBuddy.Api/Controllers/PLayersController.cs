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
    public class PLayersController : ControllerBase
    {
        //Loosely coupled
        //nameing convention 
        //IoC Container -- Inversion of Controler

        private readonly IPlayerService _playerService;

        public PLayersController(IPlayerService playerService)
        {
            _playerService = playerService;
        }

        [HttpGet("GetPlayerList")] //alyans (isim) verdik
        public List<Player> GetPlayerList()
        {
            IPlayerService playerService = new PlayerManager(new EFPlayerDal());
            var result = playerService.GetPlayers();
            return result.Data;
        }

        [HttpGet("GetPlayerById")] 
        public Player GetplayerById([FromQuery]int playerId)
        {
            IPlayerService playerService = new PlayerManager(new EFPlayerDal());
            var result = playerService.GetPlayerInfo(playerId);
            return result.Data;
        }
        [HttpPost("SavePlayer")]
        public IActionResult SavePlayer(PlayerModel playerModel)
        {
            var player= new Player()
            {
                PlayerName= playerModel.PlayerName,
                PlayerSurname= playerModel.PlayerSurname,
                Password= playerModel.Password,
                Size= playerModel.Size,
                Weight= playerModel.Weight,
                UserScore= playerModel.UserScore,
                Address= playerModel.Address,
                Age= playerModel.Age,
                Email= playerModel.Email,
                PhoneNumber= playerModel.PhoneNumber,
                UserName= playerModel.UserName,
            };
            var result = _playerService.Add(player);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }
        [HttpPost("UpdatePlayer")]
        public IActionResult UpdatePlayer(PlayerModel playerModel)
        {
            var player = new Player()
            {
                PlayerId= playerModel.PlayerId,
                PlayerName = playerModel.PlayerName,
                PlayerSurname = playerModel.PlayerSurname,
                Password = playerModel.Password,
                Size = playerModel.Size,
                Weight = playerModel.Weight,
                UserScore = playerModel.UserScore,
                Address = playerModel.Address,
                Age = playerModel.Age,
                Email = playerModel.Email,
                PhoneNumber = playerModel.PhoneNumber,
                UserName = playerModel.UserName,
            };
            var result = _playerService.Update(player);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }

        [HttpPost("PlayerLogin")]
        public Player PlayerLogin(LoginModel loginModel)
        {
            var PlayerLogin = new PlayerLogin()
            {
                Password = loginModel.Password,
                Email = loginModel.Email,
                UserName= loginModel.UserName,
            };
            IPlayerService playerService = new PlayerManager(new EFPlayerDal());
            var result = playerService.LoginToGetPlayer(PlayerLogin);
            return result.Data;
        }

        [HttpPost("AISortByPlayer")]
        public List<AIPLayerModel> AISortByPlayer(int playerId)
        {            
            IPlayerService playerService = new PlayerManager(new EFPlayerDal());
            var result = playerService.AISortByPlayer(playerId);
            return result.Data;

        }
    }
}
