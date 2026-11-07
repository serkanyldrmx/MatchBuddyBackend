using System.Text.Json;
using MatchBuddy.Api.Model;
using MatchBuddy.Api.Security;
using MatchBuddy.Api.Services;
using MatchBuddy.Business.Abstract;
using MatchBuddy.Business.Security;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MatchBuddy.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PLayersController : ControllerBase
    {
        private readonly IPlayerService _playerService;
        private readonly IJwtTokenService _tokenService;
        private readonly ILogger<PLayersController> _logger;
        private readonly IMatchParticipationService _participationService;
        private readonly ISocialService _socialService;

        private readonly IMediaService _mediaService;
        private readonly MatchBuddy.Business.Auth.IAuthService _auth;
        private readonly AuthSessionIssuer _issuer;

        public PLayersController(IPlayerService playerService, IJwtTokenService tokenService, ILogger<PLayersController> logger,
            IMatchParticipationService participationService, ISocialService socialService, IMediaService mediaService,
            MatchBuddy.Business.Auth.IAuthService auth, AuthSessionIssuer issuer)
        {
            _auth = auth;
            _issuer = issuer;
            _mediaService = mediaService;
            _socialService = socialService;
            _playerService = playerService;
            _tokenService = tokenService;
            _logger = logger;
            _participationService = participationService;
        }

        // Oyuncunun maç istatistikleri (oynadığı maçlar, gol, asist, MVP, puan)
        [HttpGet("GetPlayerStats")]
        public ActionResult<PlayerStatsDto> GetPlayerStats([FromQuery] int playerId)
        {
            var result = _participationService.GetPlayerStats(playerId);
            if (!result.Success || result.Data == null)
            {
                return this.NotFoundError(ErrorCodes.Player.NotFound);
            }
            return result.Data;
        }

        // Liderlik tablosu: admin hesaplar hariç; userScore, sonra matchesPlayed'e göre azalan
        [HttpGet("GetLeaderboard")]
        public ActionResult<List<LeaderboardRowDto>> GetLeaderboard([FromQuery] int top = 20)
        {
            // Silinmiş hesaplar ve engeller (iki yön) hariç
            return _participationService.GetLeaderboard(top, User.GetPlayerId()).Data;
        }

        // Oyuncu listesi: sadece herkese açık bilgiler (şifre, e-posta, telefon, adres yok)
        [HttpGet("GetPlayerList")]
        public ActionResult<List<PublicPlayerDto>> GetPlayerList()
        {
            // Silinmiş hesaplar ve engeller (iki yön) hariç
            return _socialService.GetVisiblePlayers(User.GetPlayerId()).Select(PublicPlayerDto.From).ToList();
        }

        // Kendi profilin (veya admin) için tam bilgi, diğerleri için herkese açık bilgi
        [HttpGet("GetPlayerById")]
        public ActionResult<PublicPlayerDto> GetplayerById([FromQuery] int playerId)
        {
            var player = _playerService.GetPlayerInfo(playerId).Data;
            if (player == null)
            {
                return this.NotFoundError(ErrorCodes.Player.NotFound);
            }

            return User.IsSelfOrAdmin(playerId) ? PlayerDto.From(player) : PublicPlayerDto.From(player);
        }

        // Kayıt ol (anonim erişim)
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [HttpPost("SavePlayer")]
        public async Task<IActionResult> SavePlayer([FromForm] string playerModel, [FromForm] IFormFile? profilePicture, CancellationToken ct)
        {
            PlayerModel? playerData;
            try
            {
                playerData = JsonSerializer.Deserialize<PlayerModel>(playerModel, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException)
            {
                return this.BadRequestError(ErrorCodes.Player.InvalidData);
            }
            if (playerData == null)
            {
                return this.BadRequestError(ErrorCodes.Player.InvalidData);
            }

            // KVKK: güncel kullanım koşulları + aydınlatma metni kabul edilmeden kayıt olunmaz (docs/auth-api.md)
            var consentError = _auth.ValidateRegistrationConsent(playerData.TermsVersion);
            if (consentError != null)
            {
                return this.FieldError("termsVersion", consentError.Code, consentError.Args);
            }

            var player = new Player
            {
                PlayerName = playerData.PlayerName,
                PlayerSurname = playerData.PlayerSurname,
                Password = playerData.Password, // PlayerManager.Add içinde hash'lenir
                Size = playerData.Size,
                Weight = playerData.Weight,
                UserScore = 0, // Puan kullanıcı tarafından belirlenemez
                Address = playerData.Address,
                Age = playerData.Age,
                Email = playerData.Email,
                PhoneNumber = playerData.PhoneNumber,
                UserName = playerData.UserName,
                IsAdmin = 0,
                ProfilePictureUrl = ""
            };
            _auth.ApplyRegistrationConsent(player, playerData.MarketingConsent);

            // Profil fotoğrafı medya hattından geçer (docs/media-api.md); oyuncu henüz yok: sahibi kayıttan sonra yazılır
            MediaDto? avatar = null;
            if (profilePicture != null)
            {
                var upload = await _mediaService.UploadAsync(profilePicture, MediaKind.Avatar, null, ct);
                if (!upload.Success) return this.ToHttp(upload);
                avatar = upload.Data;
                player.ProfilePictureUrl = avatar.Url;
                player.ProfileMediaId = avatar.MediaId;
            }

            var result = _playerService.Add(player);
            if (result.Success)
            {
                if (avatar != null) _mediaService.MarkAttached(avatar.MediaId, player.PlayerId);
                // E-posta doğrulama kodu (yumuşak zorunluluk: giriş yine çalışır)
                if (!string.IsNullOrWhiteSpace(player.Email))
                    _auth.SendEmailVerification(player.PlayerId, enforceCooldown: false, AuthSessionIssuer.Client(HttpContext));
                return Ok(new { result.Success, result.Message, player.ProfilePictureUrl });
            }
            if (avatar != null) await _mediaService.DeleteAsync(avatar.MediaId, CancellationToken.None);
            return this.ToHttp(result);
        }

        // Profil güncelle: sadece kendi profilini (admin herkesinkini)
        [HttpPost("UpdatePlayer")]
        public async Task<IActionResult> UpdatePlayer([FromForm] string playerModel, [FromForm] IFormFile? profilePicture, CancellationToken ct)
        {
            PlayerModel? playerData;
            try
            {
                playerData = JsonSerializer.Deserialize<PlayerModel>(playerModel, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException)
            {
                return this.BadRequestError(ErrorCodes.Common.InvalidJson);
            }
            if (playerData == null)
            {
                return this.BadRequestError(ErrorCodes.Common.InvalidJson);
            }

            if (!User.IsSelfOrAdmin(playerData.PlayerId))
            {
                return Forbid();
            }

            var existingPlayer = _playerService.GetPlayerInfo(playerData.PlayerId).Data;
            if (existingPlayer == null)
            {
                return this.NotFoundError(ErrorCodes.Player.NotFound);
            }

            if (!string.IsNullOrWhiteSpace(playerData.UserName) &&
                _playerService.IsUserNameTaken(playerData.UserName, existingPlayer.PlayerId))
            {
                return this.BadRequestError(ErrorCodes.Auth.UserNameTaken);
            }

            existingPlayer.PlayerName = playerData.PlayerName;
            existingPlayer.PlayerSurname = playerData.PlayerSurname;
            existingPlayer.Size = playerData.Size;
            existingPlayer.Weight = playerData.Weight;
            existingPlayer.Address = playerData.Address;
            existingPlayer.Age = playerData.Age;
            // E-posta değişirse doğrulama sıfırlanır ve yeni adrese kod gönderilir
            var emailChanged = !string.Equals((existingPlayer.Email ?? string.Empty).Trim(), (playerData.Email ?? string.Empty).Trim(),
                StringComparison.OrdinalIgnoreCase);
            if (emailChanged) existingPlayer.EmailConfirmed = false;
            existingPlayer.Email = playerData.Email;
            existingPlayer.PhoneNumber = playerData.PhoneNumber;
            existingPlayer.UserName = playerData.UserName;

            // Puanı sadece admin değiştirebilir
            if (User.IsAdmin())
            {
                existingPlayer.UserScore = playerData.UserScore;
            }

            // Şifre boş gönderildiyse mevcut şifre korunur
            var passwordChanged = false;
            if (!string.IsNullOrEmpty(playerData.Password))
            {
                var policy = MatchBuddy.Business.Auth.PasswordPolicy.Validate(playerData.Password);
                if (policy != null)
                {
                    return this.BadRequestError(policy.Code, policy.Args);
                }
                existingPlayer.Password = PasswordHasher.Hash(playerData.Password);
                existingPlayer.HasPassword = true;
                passwordChanged = true;
            }

            // Yeni profil fotoğrafı: medya hattı; başarılı güncellemeden sonra eskisi (medya ya da /uploads dosyası) silinir
            MediaDto? avatar = null;
            var oldMediaId = existingPlayer.ProfileMediaId;
            var oldUrl = existingPlayer.ProfilePictureUrl;
            if (profilePicture != null)
            {
                var upload = await _mediaService.UploadAsync(profilePicture, MediaKind.Avatar, User.GetPlayerId(), ct);
                if (!upload.Success) return this.ToHttp(upload);
                avatar = upload.Data;
                existingPlayer.ProfilePictureUrl = avatar.Url;
                existingPlayer.ProfileMediaId = avatar.MediaId;
            }

            var result = _playerService.Update(existingPlayer);
            if (result.Success)
            {
                if (avatar != null)
                {
                    _mediaService.MarkAttached(avatar.MediaId);
                    await _mediaService.DeleteImageAsync(oldMediaId, oldUrl, CancellationToken.None);
                }
                // Şifre değişti → tüm refresh token'lar iptal (docs/auth-api.md). Tercihen /api/Auth/ChangePassword kullanılmalı.
                if (passwordChanged) _auth.OnPasswordChangedExternally(existingPlayer.PlayerId);
                if (emailChanged && !string.IsNullOrWhiteSpace(existingPlayer.Email))
                    _auth.SendEmailVerification(existingPlayer.PlayerId, enforceCooldown: false, AuthSessionIssuer.Client(HttpContext));
                return Ok(new { result.Success, result.Message, existingPlayer.ProfilePictureUrl });
            }
            if (avatar != null) await _mediaService.DeleteAsync(avatar.MediaId, CancellationToken.None);
            return this.ToHttp(result);
        }

        // Giriş: başarılıysa JWT ve şifresiz profil döner, değilse 401
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [HttpPost("PlayerLogin")]
        public ActionResult<LoginResponse> PlayerLogin(LoginModel loginModel)
        {
            var result = _playerService.LoginToGetPlayer(new PlayerLogin
            {
                Password = loginModel.Password,
                Email = loginModel.Email,
                UserName = loginModel.UserName,
            });

            if (!result.Success || result.Data == null)
            {
                _logger.LogWarning("Başarısız giriş denemesi: {UserName}", loginModel.UserName);
                return this.UnauthorizedError(ErrorCodes.Auth.InvalidCredentials);
            }

            // ESKİ uç nokta (geriye uyumluluk): /api/Auth/Login ile aynı; access token eski süreyle
            // (auth.legacyAccessTokenMinutes, varsayılan 120 dk), refresh çerezi de yazılır. docs/auth-api.md.
            return _issuer.SignIn(HttpContext, result.Data, rememberMe: true,
                accessMinutes: P.Int(ParameterKeys.Auth.LegacyAccessTokenMinutes));
        }

        // Öneri servisinin kullandığı oyuncu geçmişi: sadece kendisi veya admin
        [HttpGet("AISortByPlayer")]
        public ActionResult<List<AIPLayerModel>> AISortByPlayer(int playerId)
        {
            if (!User.IsSelfOrAdmin(playerId))
            {
                return Forbid();
            }
            return _playerService.AISortByPlayer(playerId).Data;
        }
    }
}
