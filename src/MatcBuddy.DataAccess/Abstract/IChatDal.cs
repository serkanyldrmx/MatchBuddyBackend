using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.DTOs;

namespace MatcBuddy.DataAccess.Abstract
{
    /// <summary>Mesajlaşma: okundu bilgisi, okunmamış sayıları, grup özetleri ve grup üyeliği.</summary>
    public interface IChatDal
    {
        /// <summary>Karşı taraftan gelen okunmamış mesajları okundu yapar; güncellenen satır sayısını döner.</summary>
        int MarkDirectRead(int me, int otherId);
        OperationResult<bool> MarkGroupRead(int me, int groupId);
        UnreadSummaryDto Unread(int me);
        List<GroupSummaryDto> MyGroups(int me, bool isAdmin);
        OperationResult<int> CreateGroup(int me, string name, IEnumerable<int> playerIds);
        OperationResult<int> AddMembers(int me, int groupId, IEnumerable<int> playerIds);
        /// <summary>playerId == me: gruptan ayrıl; başkası: yalnızca kurucu ya da admin çıkarabilir.</summary>
        OperationResult<bool> RemoveMember(int me, int groupId, int playerId, bool isAdmin);

        // Konuşma, yanıt, tepki, silme (docs/engagement-api.md §1)
        List<ChatMessageDto> DirectConversation(int me, int otherId);
        List<ChatMessageDto> GroupConversation(int me, int groupId);
        bool IsValidReply(bool isGroup, int replyToId, int me, int otherOrGroupId);
        OperationResult<ChatTargetDto> React(int me, ReactInput input);
        OperationResult<ChatTargetDto> Delete(int me, int? messageId, int? groupMessageId);
    }
}
