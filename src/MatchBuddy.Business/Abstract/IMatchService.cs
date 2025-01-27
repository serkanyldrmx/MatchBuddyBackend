using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Business.Abstract
{
    public interface IMatchService
    {
        IDataResult<List<Match>> GetAll();
        //Tüm maçları getir       
        IDataResult<Match> GetById(int matchId);
        //seçili maç bilgilerini getir
        IResult Add(Match match);
        //Maç ekle - oluştur
        IResult Delete(Match match);
        //Maç silme
        IResult Update(Match match);
        //Maç ekle - oluştur
        IDataResult<List<MatchComentsDto>> GetMatchComents(int matchId);
        IDataResult<List<MatchTeamDto>> GetMatchTeam(int matchId);
        IResult AddMatchTeam(MatchTeam matchTeam);
        IResult MatchStatusUpdate(int matchId, byte status);

    }
}
