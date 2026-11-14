namespace MatcBuddy.Core.Errors
{
    public static partial class ErrorCodes
    {
        /// <summary>Mesaj yanıtı / tepkisi / silme (docs/engagement-api.md §1).</summary>
        public static partial class Message
        {
            public const string NotFound = "MESSAGE_NOT_FOUND";
            public const string NotYours = "MESSAGE_NOT_YOURS";
            public const string ReplyInvalid = "MESSAGE_REPLY_INVALID";
            public const string InvalidReaction = "MESSAGE_INVALID_REACTION";
            public const string AlreadyDeleted = "MESSAGE_ALREADY_DELETED";
        }

        /// <summary>Davet linki (§3).</summary>
        public static partial class Share
        {
            public const string NotFound = "SHARE_NOT_FOUND";
        }

        /// <summary>Harici saha satış listesi (§4).</summary>
        public static partial class External
        {
            public const string LinkNoVenues = "EXTERNAL_LINK_NO_VENUES";
            public const string LinkTargetInvalid = "EXTERNAL_LINK_TARGET_INVALID";
        }
    }
}
