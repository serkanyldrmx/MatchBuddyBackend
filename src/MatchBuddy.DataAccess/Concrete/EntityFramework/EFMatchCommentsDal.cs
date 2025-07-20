using MatchBuddy.Core.DataAccess.EntityFramework;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.DataAccess.Concrete.EntityFramework
{
    public class EFMatchCommentsDal : EfEntityRepositoryBase<MatchComment, MatchBuddyContext>, IMatchCommentsDal
    {
        public List<MatchComentsDto> GetMatchCommentToMatchId(int matchId)
        {
            using (MatchBuddyContext context = new MatchBuddyContext())
            {
                var result = from mc in context.MatchComments
                             join p in context.Players on mc.playerId equals p.PlayerId
                             where mc.MatchId == matchId
                             select new MatchComentsDto
                             {
                                 CommentsId = mc.CommentsId,
                                 Comment = mc.Comment,
                                 MatchId = mc.MatchId,
                                 PlayerId = mc.playerId,
                                 PlayerName = p.PlayerName,
                                 PlayerSurname = p.PlayerSurname,
                                 UserName = p.UserName,
                                 ProfilePictureUrl= p.ProfilePictureUrl
                             };

                return result.ToList();
            }
        }


    }
}
