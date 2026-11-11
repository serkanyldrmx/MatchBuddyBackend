using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatchBuddy.DataAccess.Concrete.EntityFramework
{
    /// <summary>
    /// Konuşma okuma, yanıt, tepki ve silme (docs/engagement-api.md §1). Silinen mesajın metni / resmi boşaltılır,
    /// tepkileri kaldırılır; konuşmada "Bu mesaj silindi" olarak kalır.
    /// </summary>
    public partial class EFChatDal
    {
        /// <summary>İzin verilen tepkiler: klasikler + maç emojileri.</summary>
        public static readonly IReadOnlyList<string> AllowedReactions = new[]
        {
            "👍", "❤️", "😂", "🔥", "😮", "😢", "👏", "💪", "⚽", "🥅", "🧤", "🏆", "🟨", "🟥", "🍻",
        };

        private static string Name(Player? p) =>
            p == null ? string.Empty : string.Join(' ', new[] { p.PlayerName, p.PlayerSurname }.Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } n ? n : p.UserName ?? string.Empty;

        private static string ShortText(string? text, string? imageUrl, bool deleted)
        {
            if (deleted) return Msg.Get(MessageKeys.Message.LabelDeleted);
            var t = PhotoPreview(text, imageUrl);
            return t.Length > 120 ? t[..120] + "…" : t;
        }

        private static List<ReactionSummaryDto> Summaries(IEnumerable<(string Emoji, int PlayerId, string Name)> rows, int me) =>
            rows.GroupBy(r => r.Emoji)
                .Select(g => new ReactionSummaryDto
                {
                    Emoji = g.Key,
                    Count = g.Count(),
                    Mine = g.Any(r => r.PlayerId == me),
                    Names = g.Select(r => r.Name).Take(10).ToList(),
                })
                .OrderByDescending(r => r.Count).ThenBy(r => r.Emoji, StringComparer.Ordinal)
                .ToList();

        public List<ChatMessageDto> DirectConversation(int me, int otherId)
        {
            using var context = new MatchBuddyContext();
            var list = context.Messages.AsNoTracking()
                .Where(m => (m.SendPlayerId == me && m.RecipientPlayerId == otherId) || (m.SendPlayerId == otherId && m.RecipientPlayerId == me))
                .OrderBy(m => m.SendingDate).ThenBy(m => m.MessageId)
                .ToList();
            if (list.Count == 0) return new List<ChatMessageDto>();

            var ids = list.Select(m => m.MessageId).ToList();
            var players = context.Players.AsNoTracking().Where(p => p.PlayerId == me || p.PlayerId == otherId).ToList().ToDictionary(p => p.PlayerId);
            var reactions = (from r in context.MessageReactions.AsNoTracking()
                             join p in context.Players.AsNoTracking() on r.PlayerId equals p.PlayerId
                             where r.MessageId != null && ids.Contains(r.MessageId.Value)
                             orderby r.CreatedAt
                             select new { MessageId = r.MessageId!.Value, r.Emoji, r.PlayerId, Player = p })
                .ToList()
                .GroupBy(r => r.MessageId)
                .ToDictionary(g => g.Key, g => g.Select(r => (r.Emoji, r.PlayerId, Name(r.Player))).ToList());
            var byId = list.ToDictionary(m => m.MessageId);
            // Yanıtlanan mesaj listede yoksa (olmamalı) ayrıca yüklenmez

            return list.Select(m =>
            {
                ReplyPreviewDto? reply = null;
                if (m.ReplyToMessageId.HasValue && byId.TryGetValue(m.ReplyToMessageId.Value, out var r))
                {
                    players.TryGetValue(r.SendPlayerId ?? 0, out var rp);
                    reply = new ReplyPreviewDto
                    {
                        Id = r.MessageId,
                        SendPlayerId = r.SendPlayerId,
                        SenderName = Name(rp),
                        Text = ShortText(r.MatchMessage, r.ImageUrl, r.IsDeleted),
                        HasImage = !r.IsDeleted && r.ImageUrl != null,
                        IsDeleted = r.IsDeleted,
                    };
                }
                players.TryGetValue(m.SendPlayerId ?? 0, out var sender);
                return new ChatMessageDto
                {
                    MessageId = m.MessageId,
                    MatchMessage = m.IsDeleted ? null : m.MatchMessage,
                    SendingDate = m.SendingDate,
                    SendPlayerId = m.SendPlayerId,
                    RecipientPlayerId = m.RecipientPlayerId,
                    ImageUrl = m.IsDeleted ? null : m.ImageUrl,
                    ReadAt = m.ReadAt,
                    Status = m.Status,
                    IsDeleted = m.IsDeleted,
                    UserName = sender?.UserName,
                    PlayerName = sender?.PlayerName,
                    PlayerSurname = sender?.PlayerSurname,
                    ProfilePictureUrl = sender == null || sender.IsDeleted ? null : sender.ProfilePictureUrl,
                    ReplyTo = m.IsDeleted ? null : reply,
                    Reactions = m.IsDeleted || !reactions.TryGetValue(m.MessageId, out var rs) ? new() : Summaries(rs, me),
                };
            }).ToList();
        }

        public List<ChatMessageDto> GroupConversation(int me, int groupId)
        {
            using var context = new MatchBuddyContext();
            var list = context.GroupMessages.AsNoTracking()
                .Where(m => m.GroupId == groupId)
                .OrderBy(m => m.SendingDate).ThenBy(m => m.GroupMessageId)
                .ToList();
            if (list.Count == 0) return new List<ChatMessageDto>();

            var ids = list.Select(m => m.GroupMessageId).ToList();
            var senderIds = list.Where(m => m.SendPlayerId.HasValue).Select(m => m.SendPlayerId!.Value).Distinct().ToList();
            var players = context.Players.AsNoTracking().Where(p => senderIds.Contains(p.PlayerId)).ToList().ToDictionary(p => p.PlayerId);
            var reactions = (from r in context.MessageReactions.AsNoTracking()
                             join p in context.Players.AsNoTracking() on r.PlayerId equals p.PlayerId
                             where r.GroupMessageId != null && ids.Contains(r.GroupMessageId.Value)
                             orderby r.CreatedAt
                             select new { GroupMessageId = r.GroupMessageId!.Value, r.Emoji, r.PlayerId, Player = p })
                .ToList()
                .GroupBy(r => r.GroupMessageId)
                .ToDictionary(g => g.Key, g => g.Select(r => (r.Emoji, r.PlayerId, Name(r.Player))).ToList());
            var byId = list.ToDictionary(m => m.GroupMessageId);

            return list.Select(m =>
            {
                ReplyPreviewDto? reply = null;
                if (m.ReplyToGroupMessageId.HasValue && byId.TryGetValue(m.ReplyToGroupMessageId.Value, out var r))
                {
                    players.TryGetValue(r.SendPlayerId ?? 0, out var rp);
                    reply = new ReplyPreviewDto
                    {
                        Id = r.GroupMessageId,
                        SendPlayerId = r.SendPlayerId,
                        SenderName = Name(rp),
                        Text = ShortText(r.MatchMessage, r.ImageUrl, r.IsDeleted),
                        HasImage = !r.IsDeleted && r.ImageUrl != null,
                        IsDeleted = r.IsDeleted,
                    };
                }
                players.TryGetValue(m.SendPlayerId ?? 0, out var sender);
                return new ChatMessageDto
                {
                    GroupMessageId = m.GroupMessageId,
                    GroupId = m.GroupId,
                    MatchMessage = m.IsDeleted ? null : m.MatchMessage,
                    SendingDate = m.SendingDate,
                    SendPlayerId = m.SendPlayerId,
                    ImageUrl = m.IsDeleted ? null : m.ImageUrl,
                    Status = m.Status,
                    IsDeleted = m.IsDeleted,
                    UserName = sender?.UserName,
                    PlayerName = sender?.PlayerName,
                    PlayerSurname = sender?.PlayerSurname,
                    ProfilePictureUrl = sender == null || sender.IsDeleted ? null : sender.ProfilePictureUrl,
                    ReplyTo = m.IsDeleted ? null : reply,
                    Reactions = m.IsDeleted || !reactions.TryGetValue(m.GroupMessageId, out var rs) ? new() : Summaries(rs, me),
                };
            }).ToList();
        }

        public bool IsValidReply(bool isGroup, int replyToId, int me, int otherOrGroupId)
        {
            using var context = new MatchBuddyContext();
            return isGroup
                ? context.GroupMessages.Any(m => m.GroupMessageId == replyToId && m.GroupId == otherOrGroupId)
                : context.Messages.Any(m => m.MessageId == replyToId
                                            && ((m.SendPlayerId == me && m.RecipientPlayerId == otherOrGroupId)
                                                || (m.SendPlayerId == otherOrGroupId && m.RecipientPlayerId == me)));
        }

        /// <summary>Mesajın bulunduğu konuşma; me bu konuşmanın tarafı / grubun üyesi değilse null.</summary>
        private static ChatTargetDto? TargetOf(MatchBuddyContext context, int me, int? messageId, int? groupMessageId, out bool deleted, out int senderId)
        {
            deleted = false;
            senderId = 0;
            if (messageId is > 0)
            {
                var m = context.Messages.AsNoTracking().FirstOrDefault(x => x.MessageId == messageId);
                if (m == null || (m.SendPlayerId != me && m.RecipientPlayerId != me)) return null;
                deleted = m.IsDeleted;
                senderId = m.SendPlayerId ?? 0;
                return new ChatTargetDto { IsGroup = false, SenderId = m.SendPlayerId ?? 0, RecipientId = m.RecipientPlayerId ?? 0 };
            }
            if (groupMessageId is > 0)
            {
                var g = context.GroupMessages.AsNoTracking().FirstOrDefault(x => x.GroupMessageId == groupMessageId);
                if (g == null || !context.GroupPlayers.Any(p => p.GroupId == g.GroupId && p.PlayerId == me)) return null;
                deleted = g.IsDeleted;
                senderId = g.SendPlayerId ?? 0;
                return new ChatTargetDto { IsGroup = true, GroupId = g.GroupId, SenderId = g.SendPlayerId ?? 0 };
            }
            return null;
        }

        public OperationResult<ChatTargetDto> React(int me, ReactInput input)
        {
            var emoji = string.IsNullOrWhiteSpace(input.Emoji) ? null : input.Emoji.Trim();
            if (emoji != null && !AllowedReactions.Contains(emoji))
                return OperationResult<ChatTargetDto>.BadRequest(ErrorCodes.Message.InvalidReaction);

            using var context = new MatchBuddyContext();
            var target = TargetOf(context, me, input.MessageId, input.GroupMessageId, out var deleted, out _);
            if (target == null) return OperationResult<ChatTargetDto>.NotFound(ErrorCodes.Message.NotFound);
            if (deleted) return OperationResult<ChatTargetDto>.BadRequest(ErrorCodes.Message.AlreadyDeleted);

            var existing = target.IsGroup
                ? context.MessageReactions.FirstOrDefault(r => r.GroupMessageId == input.GroupMessageId && r.PlayerId == me)
                : context.MessageReactions.FirstOrDefault(r => r.MessageId == input.MessageId && r.PlayerId == me);
            if (existing != null && (emoji == null || existing.Emoji == emoji))
            {
                context.MessageReactions.Remove(existing);
            }
            else if (existing != null)
            {
                existing.Emoji = emoji!;
                existing.CreatedAt = DateTime.Now;
            }
            else if (emoji != null)
            {
                context.MessageReactions.Add(new MessageReaction
                {
                    MessageId = target.IsGroup ? null : input.MessageId,
                    GroupMessageId = target.IsGroup ? input.GroupMessageId : null,
                    PlayerId = me,
                    Emoji = emoji,
                    CreatedAt = DateTime.Now,
                });
            }
            context.SaveChanges();
            return OperationResult<ChatTargetDto>.Ok(target);
        }

        public OperationResult<ChatTargetDto> Delete(int me, int? messageId, int? groupMessageId)
        {
            using var context = new MatchBuddyContext();
            var target = TargetOf(context, me, messageId, groupMessageId, out var deleted, out var senderId);
            if (target == null) return OperationResult<ChatTargetDto>.NotFound(ErrorCodes.Message.NotFound);
            if (senderId != me) return OperationResult<ChatTargetDto>.Forbidden(ErrorCodes.Message.NotYours);
            if (deleted) return OperationResult<ChatTargetDto>.BadRequest(ErrorCodes.Message.AlreadyDeleted);

            var now = DateTime.Now;
            if (target.IsGroup)
            {
                var g = context.GroupMessages.First(x => x.GroupMessageId == groupMessageId);
                g.IsDeleted = true;
                g.DeletedAt = now;
                g.MatchMessage = string.Empty;
                g.ImageUrl = null;
                g.MediaId = null;
                context.MessageReactions.Where(r => r.GroupMessageId == groupMessageId).ExecuteDelete();
            }
            else
            {
                var m = context.Messages.First(x => x.MessageId == messageId);
                m.IsDeleted = true;
                m.DeletedAt = now;
                m.MatchMessage = string.Empty;
                m.ImageUrl = null;
                m.MediaId = null;
                context.MessageReactions.Where(r => r.MessageId == messageId).ExecuteDelete();
            }
            context.SaveChanges();
            return OperationResult<ChatTargetDto>.Ok(target, Msg.Get(MessageKeys.Message.Deleted));
        }
    }
}
