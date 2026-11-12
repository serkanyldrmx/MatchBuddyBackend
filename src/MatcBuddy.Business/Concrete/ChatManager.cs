using MatcBuddy.Business.Abstract;
using MatcBuddy.Business.Constants;
using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.Entities.DTOs;

namespace MatcBuddy.Business.Concrete
{
    public class ChatManager : IChatService
    {
        private const int GroupNameMax = 50;
        private readonly IChatDal _chatDal;

        public ChatManager(IChatDal chatDal)
        {
            _chatDal = chatDal;
        }

        public int MarkDirectRead(int me, int otherId) => otherId > 0 ? _chatDal.MarkDirectRead(me, otherId) : 0;

        public OperationResult<bool> MarkGroupRead(int me, int groupId) => _chatDal.MarkGroupRead(me, groupId);

        public UnreadSummaryDto Unread(int me) => _chatDal.Unread(me);

        public List<GroupSummaryDto> MyGroups(int me, bool isAdmin) => _chatDal.MyGroups(me, isAdmin);

        public OperationResult<int> CreateGroup(int me, string? name, IEnumerable<int>? playerIds)
        {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > GroupNameMax)
                return OperationResult<int>.BadRequest(Messages.GroupNameInvalid);
            return _chatDal.CreateGroup(me, name, playerIds ?? Enumerable.Empty<int>());
        }

        public OperationResult<int> AddMembers(int me, int groupId, IEnumerable<int>? playerIds)
        {
            if (playerIds == null || !playerIds.Any()) return OperationResult<int>.BadRequest(ErrorCodes.Group.NoPlayers);
            return _chatDal.AddMembers(me, groupId, playerIds);
        }

        public OperationResult<bool> RemoveMember(int me, int groupId, int playerId, bool isAdmin) =>
            _chatDal.RemoveMember(me, groupId, playerId, isAdmin);

        public List<ChatMessageDto> DirectConversation(int me, int otherId) => _chatDal.DirectConversation(me, otherId);

        public List<ChatMessageDto> GroupConversation(int me, int groupId) => _chatDal.GroupConversation(me, groupId);

        public bool IsValidReply(bool isGroup, int replyToId, int me, int otherOrGroupId) =>
            replyToId > 0 && _chatDal.IsValidReply(isGroup, replyToId, me, otherOrGroupId);

        public OperationResult<ChatTargetDto> React(int me, ReactInput? input)
        {
            if (input == null || (input.MessageId is not > 0 && input.GroupMessageId is not > 0))
                return OperationResult<ChatTargetDto>.BadRequest(ErrorCodes.Message.NotFound);
            return _chatDal.React(me, input);
        }

        public OperationResult<ChatTargetDto> Delete(int me, int? messageId, int? groupMessageId)
        {
            if (messageId is not > 0 && groupMessageId is not > 0)
                return OperationResult<ChatTargetDto>.BadRequest(ErrorCodes.Message.NotFound);
            return _chatDal.Delete(me, messageId, groupMessageId);
        }
    }
}
