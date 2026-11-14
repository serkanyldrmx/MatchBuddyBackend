namespace MatcBuddy.Core.Errors
{
    public static partial class ErrorCodes
    {
        public static partial class Match
        {
            public const string OwnerCannotJoin = "MATCH_OWNER_CANNOT_JOIN";
            public const string OrganizerOnly = "MATCH_ORGANIZER_ONLY";
            public const string BalancePlayerCount = "MATCH_BALANCE_PLAYER_COUNT";   // {0} en az, {1} en fazla
            public const string OwnerCannotCreate = "MATCH_OWNER_CANNOT_CREATE";
            public const string SideFull = "MATCH_SIDE_FULL";
            public const string RejectedJoinClosed = "MATCH_REJECTED_JOIN_CLOSED";
            public const string PastJoinClosed = "MATCH_PAST_JOIN_CLOSED";
            public const string AlreadyJoined = "MATCH_ALREADY_JOINED";
            public const string AlreadyWaitlisted = "MATCH_ALREADY_WAITLISTED";
            public const string NotParticipant = "MATCH_NOT_PARTICIPANT";
            public const string PastCannotLeave = "MATCH_PAST_CANNOT_LEAVE";
            public const string ConfirmOnlyGoing = "MATCH_CONFIRM_ONLY_GOING";
            public const string PlayerNotInRoster = "MATCH_PLAYER_NOT_IN_ROSTER";
            public const string RejectedRosterLocked = "MATCH_REJECTED_ROSTER_LOCKED";
            public const string PastRosterLocked = "MATCH_PAST_ROSTER_LOCKED";
            public const string InvalidSide = "MATCH_INVALID_SIDE";
            public const string InvalidLineup = "MATCH_INVALID_LINEUP";
            public const string CapacityBelowRoster = "MATCH_CAPACITY_BELOW_ROSTER"; // {0} kadrodaki kişi sayısı
            public const string EditClosed = "MATCH_EDIT_CLOSED";
            public const string GuestNotFound = "MATCH_GUEST_NOT_FOUND";
            public const string GuestNameLength = "MATCH_GUEST_NAME_LENGTH";      // {0} en az, {1} en fazla
            public const string ResultNotPlayed = "MATCH_RESULT_NOT_PLAYED";
            public const string MvpNotOpen = "MATCH_MVP_NOT_OPEN";
            public const string MvpSelfVote = "MATCH_MVP_SELF_VOTE";
            public const string MvpVoterNotPlayed = "MATCH_MVP_VOTER_NOT_PLAYED";
            public const string MvpVotedNotPlayed = "MATCH_MVP_VOTED_NOT_PLAYED";
            public const string RatingNotOpen = "MATCH_RATING_NOT_OPEN";
            public const string RaterNotPlayed = "MATCH_RATER_NOT_PLAYED";
            public const string RatingSelf = "MATCH_RATING_SELF";
            public const string RatedNotPlayed = "MATCH_RATED_NOT_PLAYED";
            public const string RatingsRequired = "MATCH_RATINGS_REQUIRED";
            public const string CalendarInvalidRange = "MATCH_CALENDAR_INVALID_RANGE";
            public const string TeamAlreadyAdded = "MATCH_TEAM_ALREADY_ADDED";
        }

        public static partial class Comment
        {
            public const string Empty = "COMMENT_EMPTY";
            public const string NotFound = "COMMENT_NOT_FOUND";
        }

        public static partial class Notification
        {
            public const string NotFound = "NOTIFICATION_NOT_FOUND";
        }
    }
}
