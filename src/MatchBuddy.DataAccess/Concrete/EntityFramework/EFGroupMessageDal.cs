using MatchBuddy.Core.DataAccess.EntityFramework;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatchBuddy.DataAccess.Concrete.EntityFramework
{
    public class EFGroupMessageDal : EfEntityRepositoryBase<GroupMessage, MatchBuddyContext>, IGroupMessageDal
    {
        public List<GroupMessageModelDto> GetGroupMessageById(int groupId)
        {

            using (MatchBuddyContext context = new MatchBuddyContext())
            {
                var result = from p in context.GroupMessages
                             join pl in context.Players on p.SendPlayerId equals pl.PlayerId
                             where p.GroupId == groupId
                             orderby p.SendingDate, p.GroupMessageId
                             select new GroupMessageModelDto
                             {
                                 GroupMessageId = p.GroupMessageId,
                                 GroupId = p.GroupId,
                                 MatchMessage = p.MatchMessage,
                                 SendingDate = p.SendingDate,
                                 SendPlayerId = p.SendPlayerId,
                                 Status = p.Status,
                                 ImageUrl = p.ImageUrl,
                                 UserName = pl.UserName,
                                 PlayerName = pl.PlayerName,
                                 PlayerSurname = pl.PlayerSurname,
                                 ProfilePictureUrl = pl.IsDeleted ? null : pl.ProfilePictureUrl
                             };
                return result.ToList();
            }

        }

        //public MatchComment GetMatchComents2()
        //{
        //    using (MatchBuddyContext context = new MatchBuddyContext())
        //    {
        //        context.Matchs.Include(x => x.MatchComments).ToList();
        //        return context.MatchComments.Include(x => x.Match).ThenInclude(x => x.Stadium).FirstOrDefault();

        //    }
        //}
    }
}
