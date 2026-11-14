using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatcBuddy.DataAccess.Concrete.EntityFramework
{
    /// <summary>
    /// Mesajlaşma ekranı (docs/social-api.md "Mesajlaşma"): okundu bilgisi, okunmamış sayıları, grup özetleri ve
    /// grup üyeliği. Birebir mesajda okundu = Messages.ReadAt; grupta üyenin LastReadAt'inden sonra gelen mesajlar okunmamıştır.
    /// </summary>
    public partial class EFChatDal : IChatDal
    {
        private static string PhotoPreview(string? text, string? imageUrl) =>
            imageUrl != null && string.IsNullOrWhiteSpace(text) ? Msg.Get(MessageKeys.Message.LabelPhotoPreview) : text ?? string.Empty;

        // ------------------------------------------------------------------ Okundu / okunmamış

        public int MarkDirectRead(int me, int otherId)
        {
            using var context = new MatcBuddyContext();
            var now = DateTime.Now;
            return context.Messages
                .Where(m => m.RecipientPlayerId == me && m.SendPlayerId == otherId && m.ReadAt == null)
                .ExecuteUpdate(s => s.SetProperty(m => m.ReadAt, now));
        }

        public OperationResult<bool> MarkGroupRead(int me, int groupId)
        {
            using var context = new MatcBuddyContext();
            var row = context.GroupPlayers.FirstOrDefault(g => g.GroupId == groupId && g.PlayerId == me);
            if (row == null) return OperationResult<bool>.Forbidden(ErrorCodes.Group.NotMember);
            row.LastReadAt = DateTime.Now;
            context.SaveChanges();
            return OperationResult<bool>.Ok(true);
        }

        public UnreadSummaryDto Unread(int me)
        {
            using var context = new MatcBuddyContext();
            var blocked = SocialGraph.Load(context, me).BlockedEither.ToList();
            var direct = context.Messages.AsNoTracking()
                .Count(m => m.RecipientPlayerId == me && m.ReadAt == null && !m.IsDeleted && m.SendPlayerId != null && !blocked.Contains(m.SendPlayerId.Value));
            var groups = (from gp in context.GroupPlayers.AsNoTracking()
                          join gm in context.GroupMessages.AsNoTracking() on gp.GroupId equals gm.GroupId
                          where gp.PlayerId == me && gm.SendPlayerId != me && !gm.IsDeleted
                                && (gp.LastReadAt == null || gm.SendingDate > gp.LastReadAt)
                          select gm.GroupMessageId).Count();
            return new UnreadSummaryDto { Direct = direct, Groups = groups };
        }

        // ------------------------------------------------------------------ Grup listesi

        public List<GroupSummaryDto> MyGroups(int me, bool isAdmin)
        {
            using var context = new MatcBuddyContext();
            var memberships = context.GroupPlayers.AsNoTracking().Where(g => g.PlayerId == me)
                .Select(g => new { g.GroupId, g.LastReadAt }).ToList();
            var groupIds = memberships.Select(m => m.GroupId).ToList();
            var groups = context.Groups.AsNoTracking().Where(g => groupIds.Contains(g.GroupId)).ToList();
            if (groups.Count == 0) return new List<GroupSummaryDto>();

            var members = (from gp in context.GroupPlayers.AsNoTracking()
                           join p in context.Players.AsNoTracking() on gp.PlayerId equals p.PlayerId
                           where groupIds.Contains(gp.GroupId) && !p.IsDeleted
                           orderby gp.GroupPlayerId
                           select new { gp.GroupId, Player = p }).ToList()
                .GroupBy(x => x.GroupId)
                .ToDictionary(g => g.Key, g => g.Select(x => PlayerSummaryDto.FromPlayer(x.Player)).ToList());

            // Grup başına son mesaj (tek sorgu: en büyük id)
            var lastIds = context.GroupMessages.AsNoTracking()
                .Where(m => groupIds.Contains(m.GroupId))
                .GroupBy(m => m.GroupId)
                .Select(g => g.Max(m => m.GroupMessageId))
                .ToList();
            var lastMessages = (from m in context.GroupMessages.AsNoTracking()
                                join p in context.Players.AsNoTracking() on m.SendPlayerId equals p.PlayerId into sp
                                from p in sp.DefaultIfEmpty()
                                where lastIds.Contains(m.GroupMessageId)
                                select new { m.GroupId, m.MatchMessage, m.ImageUrl, m.SendingDate, m.SendPlayerId, m.IsDeleted, p.PlayerName, p.UserName })
                .ToList()
                .ToDictionary(m => m.GroupId);

            var unread = (from gp in context.GroupPlayers.AsNoTracking()
                          join gm in context.GroupMessages.AsNoTracking() on gp.GroupId equals gm.GroupId
                          where gp.PlayerId == me && gm.SendPlayerId != me && !gm.IsDeleted
                                && (gp.LastReadAt == null || gm.SendingDate > gp.LastReadAt)
                          group gm by gm.GroupId into g
                          select new { GroupId = g.Key, Count = g.Count() })
                .ToDictionary(x => x.GroupId, x => x.Count);

            return groups.Select(g =>
                {
                    var list = members.TryGetValue(g.GroupId, out var ms) ? ms : new List<PlayerSummaryDto>();
                    lastMessages.TryGetValue(g.GroupId, out var last);
                    return new GroupSummaryDto
                    {
                        GroupId = g.GroupId,
                        GroupName = g.GroupName,
                        CreatedByPlayerId = g.CreatedByPlayerId,
                        MemberCount = list.Count,
                        Members = list,
                        UnreadCount = unread.TryGetValue(g.GroupId, out var c) ? c : 0,
                        LastMessage = last == null
                            ? null
                            : new LastMessageDto
                            {
                                Text = last.IsDeleted ? Msg.Get(MessageKeys.Message.LabelDeleted) : PhotoPreview(last.MatchMessage, last.ImageUrl),
                                SendingDate = last.SendingDate,
                                Mine = last.SendPlayerId == me,
                                HasImage = !last.IsDeleted && last.ImageUrl != null,
                                SenderName = last.SendPlayerId == me ? null : (string.IsNullOrWhiteSpace(last.PlayerName) ? last.UserName : last.PlayerName),
                            },
                    };
                })
                .OrderByDescending(g => g.LastMessage?.SendingDate ?? DateTime.MinValue)
                .ThenBy(g => g.GroupName)
                .ToList();
        }

        // ------------------------------------------------------------------ Grup üyeliği

        /// <summary>Eklenebilir oyuncular: var olan, silinmemiş, işletme olmayan ve benimle aramızda engel olmayanlar.</summary>
        private static List<int> AddablePlayers(MatcBuddyContext context, int me, IEnumerable<int> ids)
        {
            var wanted = ids.Where(id => id > 0 && id != me).Distinct().ToList();
            if (wanted.Count == 0) return wanted;
            var blocked = SocialGraph.Load(context, me).BlockedEither;
            return context.Players.AsNoTracking()
                .Where(p => wanted.Contains(p.PlayerId) && !p.IsDeleted && p.Role != PlayerRoles.Owner)
                .Select(p => p.PlayerId)
                .ToList()
                .Where(id => !blocked.Contains(id))
                .ToList();
        }

        public OperationResult<int> CreateGroup(int me, string name, IEnumerable<int> playerIds)
        {
            using var context = new MatcBuddyContext();
            using var transaction = context.Database.BeginTransaction();
            var ids = AddablePlayers(context, me, playerIds);
            if (ids.Count == 0) return OperationResult<int>.BadRequest(ErrorCodes.Group.NoPlayers);

            var group = new Group { GroupName = name, CreatedByPlayerId = me };
            context.Groups.Add(group);
            context.SaveChanges();
            var now = DateTime.Now;
            context.GroupPlayers.Add(new GroupPlayer { GroupId = group.GroupId, PlayerId = me, LastReadAt = now });
            foreach (var id in ids) context.GroupPlayers.Add(new GroupPlayer { GroupId = group.GroupId, PlayerId = id });
            context.SaveChanges();
            transaction.Commit();
            return OperationResult<int>.Ok(group.GroupId);
        }

        public OperationResult<int> AddMembers(int me, int groupId, IEnumerable<int> playerIds)
        {
            using var context = new MatcBuddyContext();
            if (!context.Groups.Any(g => g.GroupId == groupId)) return OperationResult<int>.NotFound(ErrorCodes.Group.NotFound);
            var current = context.GroupPlayers.Where(g => g.GroupId == groupId).Select(g => g.PlayerId).ToHashSet();
            if (!current.Contains(me)) return OperationResult<int>.Forbidden(ErrorCodes.Group.NotMember);
            var ids = AddablePlayers(context, me, playerIds).Where(id => !current.Contains(id)).ToList();
            if (ids.Count == 0) return OperationResult<int>.BadRequest(ErrorCodes.Group.NoPlayers);
            foreach (var id in ids) context.GroupPlayers.Add(new GroupPlayer { GroupId = groupId, PlayerId = id });
            context.SaveChanges();
            return OperationResult<int>.Ok(ids.Count, Msg.Get(MessageKeys.Group.MembersAdded, ids.Count));
        }

        public OperationResult<bool> RemoveMember(int me, int groupId, int playerId, bool isAdmin)
        {
            using var context = new MatcBuddyContext();
            var group = context.Groups.FirstOrDefault(g => g.GroupId == groupId);
            if (group == null) return OperationResult<bool>.NotFound(ErrorCodes.Group.NotFound);
            var leaving = playerId == me;
            if (!leaving && !isAdmin && group.CreatedByPlayerId != me) return OperationResult<bool>.Forbidden(ErrorCodes.Group.CreatorOnly);
            var row = context.GroupPlayers.FirstOrDefault(g => g.GroupId == groupId && g.PlayerId == playerId);
            if (row == null) return OperationResult<bool>.NotFound(ErrorCodes.Group.NotMember);
            context.GroupPlayers.Remove(row);
            context.SaveChanges();

            // Son üye de ayrıldıysa grup ve mesajları silinir (GroupMessages FK Restrict: önce mesajlar)
            if (!context.GroupPlayers.Any(g => g.GroupId == groupId))
            {
                context.GroupMessages.Where(m => m.GroupId == groupId).ExecuteDelete();
                context.Groups.Remove(group);
                context.SaveChanges();
            }
            return OperationResult<bool>.Ok(true, Msg.Get(leaving ? MessageKeys.Group.Left : MessageKeys.Group.MemberRemoved));
        }
    }
}
