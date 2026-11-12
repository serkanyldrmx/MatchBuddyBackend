using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.DTOs;

namespace MatcBuddy.Business.Abstract
{
    /// <summary>Mesajlaşma: okundu bilgisi, okunmamış sayıları, grup özetleri ve grup üyeliği.</summary>
    public interface IChatService
    {
        int MarkDirectRead(int me, int otherId);
        OperationResult<bool> MarkGroupRead(int me, int groupId);
        UnreadSummaryDto Unread(int me);
        List<GroupSummaryDto> MyGroups(int me, bool isAdmin);
        OperationResult<int> CreateGroup(int me, string? name, IEnumerable<int>? playerIds);
        OperationResult<int> AddMembers(int me, int groupId, IEnumerable<int>? playerIds);
        OperationResult<bool> RemoveMember(int me, int groupId, int playerId, bool isAdmin);

        List<ChatMessageDto> DirectConversation(int me, int otherId);
        List<ChatMessageDto> GroupConversation(int me, int groupId);
        bool IsValidReply(bool isGroup, int replyToId, int me, int otherOrGroupId);
        OperationResult<ChatTargetDto> React(int me, ReactInput? input);
        OperationResult<ChatTargetDto> Delete(int me, int? messageId, int? groupMessageId);
    }
}
