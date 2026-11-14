using MatcBuddy.Core.DataAccess.EntityFramework;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.DataAccess.Concrete.EntityFramework
{
    public class EFMatchCommentsDal : EfEntityRepositoryBase<MatchComment, MatcBuddyContext>, IMatchCommentsDal
    {
        public List<MatchComentsDto> GetMatchCommentToMatchId(int matchId)
        {
            using (MatcBuddyContext context = new MatcBuddyContext())
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
