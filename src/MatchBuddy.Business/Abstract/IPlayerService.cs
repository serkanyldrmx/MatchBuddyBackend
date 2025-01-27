using MatchBuddy.Api.Model;
using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Business.Abstract
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
        //Yapay zekaya göre sıralama
    }
}
