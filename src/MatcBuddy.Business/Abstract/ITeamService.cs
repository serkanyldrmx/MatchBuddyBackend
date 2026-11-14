using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Business.Abstract
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
        // Takımı silme / kadroyu düzenleme yetkisi: kurucu; kurucusu bilinmeyen eski takımlarda her üye
        bool CanManage(int teamId, int playerId);
        // Takımdan ayrıl (kurucu ayrılırsa kuruculuk en eski üyeye geçer)
        IResult Leave(int teamId, int playerId);
    }
}
