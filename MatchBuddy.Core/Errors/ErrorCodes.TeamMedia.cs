namespace MatchBuddy.Core.Errors
{
    public static partial class ErrorCodes
    {
        public static partial class Tournament
        {
            public const string InputRequired = "TOURNAMENT_INPUT_REQUIRED";
            public const string NameRequired = "TOURNAMENT_NAME_REQUIRED";
            public const string InvalidFormat = "TOURNAMENT_INVALID_FORMAT";
            public const string TeamCountOutOfRange = "TOURNAMENT_TEAM_COUNT_OUT_OF_RANGE";   // {0} en az, {1} en fazla
            public const string StartDateRequired = "TOURNAMENT_START_DATE_REQUIRED";
            public const string StartDateInPast = "TOURNAMENT_START_DATE_IN_PAST";
            public const string ScoresRequired = "TOURNAMENT_SCORES_REQUIRED";
            public const string ScoreOutOfRange = "TOURNAMENT_SCORE_OUT_OF_RANGE";             // {0} en az, {1} en fazla
            public const string MatchDateRequired = "TOURNAMENT_MATCH_DATE_REQUIRED";
            public const string FixtureNotFound = "TOURNAMENT_FIXTURE_NOT_FOUND";
            public const string StadiumNotFound = "TOURNAMENT_STADIUM_NOT_FOUND";
            public const string RegisterNotTeamMember = "TOURNAMENT_REGISTER_NOT_TEAM_MEMBER";
            public const string RegistrationClosed = "TOURNAMENT_REGISTRATION_CLOSED";
            public const string TeamAlreadyRegistered = "TOURNAMENT_TEAM_ALREADY_REGISTERED";
            public const string Full = "TOURNAMENT_FULL";
            public const string TeamNotRegistered = "TOURNAMENT_TEAM_NOT_REGISTERED";
            public const string WithdrawForbidden = "TOURNAMENT_WITHDRAW_FORBIDDEN";
            public const string WithdrawAfterStart = "TOURNAMENT_WITHDRAW_AFTER_START";
            public const string StartForbidden = "TOURNAMENT_START_FORBIDDEN";
            public const string AlreadyStarted = "TOURNAMENT_ALREADY_STARTED";
            public const string LeagueMinTeams = "TOURNAMENT_LEAGUE_MIN_TEAMS";                // {0} en az takım
            public const string KnockoutMinTeams = "TOURNAMENT_KNOCKOUT_MIN_TEAMS";            // {0} en az takım
            public const string ResultForbidden = "TOURNAMENT_RESULT_FORBIDDEN";
            public const string ResultNotOngoing = "TOURNAMENT_RESULT_NOT_ONGOING";
            public const string FixtureTeamsUndetermined = "TOURNAMENT_FIXTURE_TEAMS_UNDETERMINED";
            public const string DrawWinnerRequired = "TOURNAMENT_DRAW_WINNER_REQUIRED";
            public const string WinnerNotInFixture = "TOURNAMENT_WINNER_NOT_IN_FIXTURE";
            public const string WinnerLocked = "TOURNAMENT_WINNER_LOCKED";
            public const string FixtureDateForbidden = "TOURNAMENT_FIXTURE_DATE_FORBIDDEN";
            public const string ByeHasNoDate = "TOURNAMENT_BYE_HAS_NO_DATE";
            public const string DeleteForbidden = "TOURNAMENT_DELETE_FORBIDDEN";
            public const string DeleteOnlyRegistration = "TOURNAMENT_DELETE_ONLY_REGISTRATION";
        }

        public static partial class Team
        {
            public const string OwnerCannotCreate = "TEAM_OWNER_CANNOT_CREATE";
            public const string OwnerNotAllowed = "TEAM_OWNER_NOT_ALLOWED";
            public const string PlayerIdsOrTeamIdRequired = "TEAM_PLAYER_IDS_OR_TEAM_ID_REQUIRED";
            public const string PlayerAlreadyInTeam = "TEAM_PLAYER_ALREADY_IN_TEAM";
        }

        public static partial class Media
        {
            public const string NotFound = "MEDIA_NOT_FOUND";
            public const string NoFile = "MEDIA_NO_FILE";
            public const string InvalidImage = "MEDIA_INVALID_IMAGE";
            public const string TooLarge = "MEDIA_TOO_LARGE";                 // {0} MB
            public const string TooManyPixels = "MEDIA_TOO_MANY_PIXELS";      // {0} MP
            public const string InvalidKind = "MEDIA_INVALID_KIND";
            public const string FileEmpty = "MEDIA_FILE_EMPTY";
            public const string FormatNotAllowed = "MEDIA_FORMAT_NOT_ALLOWED";
            public const string FileTooLarge = "MEDIA_FILE_TOO_LARGE";        // {0} MB
        }
    }
}
