namespace MatchBuddy.Core.Errors
{
    public static partial class ErrorCodes
    {
        public static partial class Auth
        {
            public const string UserNameRequired = "AUTH_USERNAME_REQUIRED";
            public const string UserNameTaken = "AUTH_USERNAME_TAKEN";
            public const string PasswordTooShort = "AUTH_PASSWORD_TOO_SHORT";        // {0} en az karakter
            public const string PasswordRequired = "AUTH_PASSWORD_REQUIRED";
            public const string PasswordWrong = "AUTH_PASSWORD_WRONG";
        }

        public static partial class Player
        {
            public const string InvalidData = "PLAYER_INVALID_DATA";
        }

        public static partial class Social
        {
            public const string InvalidReportReason = "SOCIAL_INVALID_REPORT_REASON"; // {0} geçerli değerler
            public const string SettingsRequired = "SOCIAL_SETTINGS_REQUIRED";
            public const string InvalidSettingChoice = "SOCIAL_INVALID_SETTING_CHOICE"; // {0} alan adı
            public const string CannotFollowSelf = "SOCIAL_CANNOT_FOLLOW_SELF";
            public const string OwnerCannotFollow = "SOCIAL_OWNER_CANNOT_FOLLOW";
            public const string OwnerNotFollowable = "SOCIAL_OWNER_NOT_FOLLOWABLE";
            public const string FollowBlocked = "SOCIAL_FOLLOW_BLOCKED";
            public const string ListPrivate = "SOCIAL_LIST_PRIVATE";
            public const string CannotBlockSelf = "SOCIAL_CANNOT_BLOCK_SELF";
            public const string CannotReportSelf = "SOCIAL_CANNOT_REPORT_SELF";
            public const string ReportNotFound = "SOCIAL_REPORT_NOT_FOUND";
        }

        public static partial class Message
        {
            public const string NotAllowed = "MESSAGE_NOT_ALLOWED";
            public const string Blocked = "MESSAGE_BLOCKED";
            public const string Empty = "MESSAGE_EMPTY";
        }

        public static partial class Invite
        {
            public const string NoPlayers = "INVITE_NO_PLAYERS";
            public const string BatchLimit = "INVITE_BATCH_LIMIT";                 // {0} en fazla
            public const string InvalidKind = "INVITE_INVALID_KIND";
            public const string NotFound = "INVITE_NOT_FOUND";
            public const string MatchRejected = "INVITE_MATCH_REJECTED";
            public const string MatchPast = "INVITE_MATCH_PAST";
            public const string NotPlayingInMatch = "INVITE_NOT_PLAYING_IN_MATCH";
            public const string NotTeamMember = "INVITE_NOT_TEAM_MEMBER";
            public const string NotYours = "INVITE_NOT_YOURS";
            public const string AlreadyResponded = "INVITE_ALREADY_RESPONDED";
            public const string PastMatchAccept = "INVITE_PAST_MATCH_ACCEPT";
        }
    }
}
