using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Business.Abstract
{
    public interface ITeamService
    {
        IDataResult<List<Team>> GetAll();
        //Tüm Takımları getirmek için
        IResult Update(Team team);
        //Bilgilerimi güncelle
        IResult Add(Team team);
        // Takım ekle + TeamCreated aktivitesi (kurucu = giriş yapan oyuncu)
        IResult Add(Team team, int creatorPlayerId);
        //Oyuncu ekleme
        IResult Delete(Team team);
        //Maç silme
        IDataResult<List<GetTeamAndPlayer>> GetTeamAndPlayer();
        IResult AddPlayerById(List<int> playerIds, int teamId);
        bool IsPlayerInTeam(int teamId, int playerId);
    }
}
