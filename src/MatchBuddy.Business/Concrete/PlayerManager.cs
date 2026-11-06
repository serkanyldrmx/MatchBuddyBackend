using MatchBuddy.Api.Model;
using MatchBuddy.Business.Abstract;
using MatchBuddy.Business.Constants;
using MatchBuddy.Business.Security;
using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Business.Concrete
{
    public class PlayerManager :IPlayerService
    {
        IPlayerDal _playerDal;
        public PlayerManager(IPlayerDal playerDal)
        {
            _playerDal = playerDal;
        }

        public IResult Add(Player player)
        {
            //business codes
            if (player.PlayerName == null || player.PlayerName.Length < 3)
            {
                return new ErrorResult(Messages.PlayerNameInvalid);
            }
            if (string.IsNullOrWhiteSpace(player.UserName))
            {
                return new ErrorResult("Kullanıcı adı zorunludur.");
            }
            if (string.IsNullOrEmpty(player.Password) || player.Password.Length < 6)
            {
                return new ErrorResult("Şifre en az 6 karakter olmalıdır.");
            }
            if (IsUserNameTaken(player.UserName))
            {
                return new ErrorResult("Bu kullanıcı adı zaten kullanılıyor.");
            }
            // Yeni kayıtlar asla admin olarak oluşturulamaz, şifre hash'lenerek saklanır
            player.IsAdmin = 0;
            player.Role = PlayerRoles.Player;
            player.Password = PasswordHasher.Hash(player.Password);
            _playerDal.Add(player);
            return new Result(true, Messages.Added);
        }

        public IDataResult<Player> GetPlayerInfo(int playerId)
        {
            return new SuccessDataResult<Player>(_playerDal.Get(p => p.PlayerId == playerId));
        }

        public IDataResult<List<Player>> GetPlayers()
        {
            //iş Kodları
            //yetkisi varmı? 

            //if (DateTime.Now.Hour == 22)
            //{
            //    return new ErrorDataResult<List<Player>>(Messages.MaintenanceTime);
            //}
            return new SuccessDataResult<List<Player>>(_playerDal.GetAll(), Messages.PlayersListed);

        }

        public IDataResult<Player> GetTeam(int playerId)
        {
            return new SuccessDataResult<Player>(_playerDal.Get(p => p.PlayerId == playerId));
        }

        public IResult Update(Player player)
        {
            if (player.PlayerName == null || player.PlayerName.Length < 2)
            {
                return new ErrorResult(Messages.PlayerNameInvalid);
            }
            _playerDal.Update(player);
            return new Result(true, Messages.Update);
        }

        public IDataResult<Player> LoginToGetPlayer(PlayerLogin playerLogin)
        {
            if (string.IsNullOrWhiteSpace(playerLogin.UserName) || string.IsNullOrEmpty(playerLogin.Password))
            {
                return new ErrorDataResult<Player>("Kullanıcı adı veya şifre hatalı.");
            }

            var player = _playerDal.Get(p => p.UserName == playerLogin.UserName);
            // Silinmiş hesap giriş yapamaz
            if (player == null || player.IsDeleted || !PasswordHasher.Verify(playerLogin.Password, player.Password, out var needsRehash))
            {
                return new ErrorDataResult<Player>("Kullanıcı adı veya şifre hatalı.");
            }

            // Eski düz metin şifreyi ilk başarılı girişte hash'li hale getir
            if (needsRehash)
            {
                player.Password = PasswordHasher.Hash(playerLogin.Password);
                _playerDal.Update(player);
            }

            return new SuccessDataResult<Player>(player);
        }

        public bool IsUserNameTaken(string userName, int? exceptPlayerId = null)
        {
            var existing = _playerDal.Get(p => p.UserName == userName);
            return existing != null && existing.PlayerId != exceptPlayerId;
        }

        public IDataResult<List<AIPLayerModel>> AISortByPlayer(int playerId)
        {
            return new SuccessDataResult<List<AIPLayerModel>>(_playerDal.AISortByPlayer(playerId));
        }
    }
}
