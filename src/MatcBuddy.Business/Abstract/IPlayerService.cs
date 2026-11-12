using MatcBuddy.Api.Model;
using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Business.Abstract
{
    public interface IPlayerService
    {
        IDataResult<Player> GetPlayerInfo(int playerId);
        //bilgilerimi getir
        IDataResult<Player> LoginToGetPlayer(PlayerLogin playerLogin);
        IDataResult<List<Player>> GetPlayers();
        //Oyuncuları Listele
        IDataResult<Player> GetTeam(int playerId);
        //Takım arkadaşlarını getir
        IResult Update(Player player);
        //Bilgilerimi güncelle
        IResult Add(Player player);
        //Oyuncu ekleme
        IDataResult<List<AIPLayerModel>> AISortByPlayer(int playerId);
        bool IsUserNameTaken(string userName, int? exceptPlayerId = null);
        //Yapay zekaya göre sıralama
    }
}
