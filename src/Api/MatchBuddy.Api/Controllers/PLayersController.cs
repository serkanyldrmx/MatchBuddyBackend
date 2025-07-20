using System.Linq;
using System.Text.Json;
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
        public IActionResult SavePlayer([FromForm] string playerModel, [FromForm] IFormFile profilePicture)
        {
            try
            {
                var playerData = JsonSerializer.Deserialize<PlayerModel>(playerModel, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var player = new Player
                {
                    PlayerName = playerData.PlayerName,
                    PlayerSurname = playerData.PlayerSurname,
                    Password = playerData.Password,
                    Size = playerData.Size,
                    Weight = playerData.Weight,
                    UserScore = playerData.UserScore,
                    Address = playerData.Address,
                    Age = playerData.Age,
                    Email = playerData.Email,
                    PhoneNumber = playerData.PhoneNumber,
                    UserName = playerData.UserName,
                    ProfilePictureUrl = ""
                };

                string errorMessage;
                var imageUrl = UploadProfilePicture(profilePicture, out errorMessage);
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    return BadRequest(new { Success = false, Message = errorMessage });
                }

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    player.ProfilePictureUrl = imageUrl;
                }

                var result = _playerService.Add(player);
                if (result.Success)
                {
                    return Ok(result);
                }
                return BadRequest(result);
            }
            catch (JsonException)
            {
                return BadRequest(new { Success = false, Message = "Invalid player model JSON." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = $"An error occurred: {ex.Message}" });
            }
        }


        [HttpPost("UpdatePlayer")]
        public IActionResult UpdatePlayer([FromForm] string playerModel, [FromForm] IFormFile profilePicture)
        {
            try
            {
                var playerData = JsonSerializer.Deserialize<PlayerModel>(playerModel, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // Oyuncu veritabanında var mı kontrol et
                var existingPlayerResult = _playerService.GetPlayerInfo(playerData.PlayerId);
                if (!existingPlayerResult.Success || existingPlayerResult.Data == null)
                {
                    return NotFound(new { Success = false, Message = "Oyuncu bulunamadı." });
                }

                var existingPlayer = existingPlayerResult.Data;

                // Yeni gelen verilerle güncelle
                existingPlayer.PlayerName = playerData.PlayerName;
                existingPlayer.PlayerSurname = playerData.PlayerSurname;
                existingPlayer.Password = playerData.Password;
                existingPlayer.Size = playerData.Size;
                existingPlayer.Weight = playerData.Weight;
                existingPlayer.UserScore = playerData.UserScore;
                existingPlayer.Address = playerData.Address;
                existingPlayer.Age = playerData.Age;
                existingPlayer.Email = playerData.Email;
                existingPlayer.PhoneNumber = playerData.PhoneNumber;
                existingPlayer.UserName = playerData.UserName;

                string errorMessage;
                var imageUrl = UploadProfilePicture(profilePicture, out errorMessage);
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    return BadRequest(new { Success = false, Message = errorMessage });
                }

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    existingPlayer.ProfilePictureUrl = imageUrl;
                }

                var result = _playerService.Update(existingPlayer);
                if (result.Success)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (JsonException)
            {
                return BadRequest(new { Success = false, Message = "Geçersiz JSON formatı." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Success = false, Message = $"Bir hata oluştu: {ex.Message}" });
            }
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

        [HttpGet("AISortByPlayer")]
        public List<AIPLayerModel> AISortByPlayer(int playerId)
        {            
            IPlayerService playerService = new PlayerManager(new EFPlayerDal());
            var result = playerService.AISortByPlayer(playerId);
            return result.Data;

        }

        private string? UploadProfilePicture(IFormFile profilePicture, out string errorMessage)
        {
            errorMessage = "";

            if (profilePicture == null) return null;

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(profilePicture.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                errorMessage = "Only JPG, JPEG, or PNG files are allowed.";
                return null;
            }

            if (profilePicture.Length > 5 * 1024 * 1024)
            {
                errorMessage = "File size must not exceed 5MB.";
                return null;
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var fileName = Guid.NewGuid().ToString() + extension;
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                profilePicture.CopyTo(stream);
            }

            return $"/Uploads/{fileName}";
        }

    }
}
