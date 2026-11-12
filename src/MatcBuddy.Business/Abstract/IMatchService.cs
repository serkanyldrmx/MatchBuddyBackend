using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Business.Abstract
{
    public interface IMatchService
    {
        IDataResult<List<Match>> GetAll();
        //Tüm maçları getir       
        IDataResult<Match> GetById(int matchId);
        //seçili maç bilgilerini getir
        IResult Add(Match match, int creatorPlayerId);
        //Maç ekle - oluştur (kurucu otomatik Going olarak eklenir)
        IResult Delete(Match match);
        //Maç silme
        IResult Update(Match match);
        IResult MatchLiked(int matchId);
        //Maç ekle - oluştur
        IDataResult<List<MatchComentsDto>> GetMatchComents(int matchId);
        IDataResult<List<MatchTeamDto>> GetMatchTeam(int matchId);
        IResult AddMatchTeam(MatchTeam matchTeam);
        IResult MatchStatusUpdate(int matchId, byte status);

    }
}
