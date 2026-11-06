namespace MatchBuddy.Core.Errors
{
    public static partial class ErrorCodes
    {
        /// <summary>Maç kurma (rezervasyon), rakip takım, rezervasyon durumu, tesis değerlendirmesi.</summary>
        public static partial class Reservation
        {
            public const string OwnerCannotCreate = "RESERVATION_OWNER_CANNOT_CREATE";
            public const string VenueClosed = "RESERVATION_VENUE_CLOSED";
            public const string PitchUnavailable = "RESERVATION_PITCH_UNAVAILABLE";
            public const string CleatsExceedPlayers = "RESERVATION_CLEATS_EXCEED_PLAYERS";
            public const string HomeTeamNotFound = "RESERVATION_HOME_TEAM_NOT_FOUND";
            public const string NotHomeTeamMember = "RESERVATION_NOT_HOME_TEAM_MEMBER";
            public const string SameTeams = "RESERVATION_SAME_TEAMS";
            public const string AwayTeamNotFound = "RESERVATION_AWAY_TEAM_NOT_FOUND";
            public const string OpponentFlagConflict = "RESERVATION_OPPONENT_FLAG_CONFLICT";
            public const string NotLookingForOpponent = "RESERVATION_NOT_LOOKING_FOR_OPPONENT";
            public const string MatchRejectedClosed = "RESERVATION_MATCH_REJECTED_CLOSED";
            public const string MatchPastClosed = "RESERVATION_MATCH_PAST_CLOSED";
            public const string HomeTeamCannotBeOpponent = "RESERVATION_HOME_TEAM_CANNOT_BE_OPPONENT";
            public const string NotTeamMember = "RESERVATION_NOT_TEAM_MEMBER";
            public const string InvalidStatus = "RESERVATION_INVALID_STATUS";
            public const string NotPending = "RESERVATION_NOT_PENDING";
            public const string ReviewNotVenueMatch = "RESERVATION_REVIEW_NOT_VENUE_MATCH";
            public const string ReviewMatchRejected = "RESERVATION_REVIEW_MATCH_REJECTED";
            public const string ReviewMatchNotPlayed = "RESERVATION_REVIEW_MATCH_NOT_PLAYED";
            public const string ReviewNotParticipant = "RESERVATION_REVIEW_NOT_PARTICIPANT";
            public const string ReviewSaveFailed = "RESERVATION_REVIEW_SAVE_FAILED";
        }

        /// <summary>İşletme paneli.</summary>
        public static partial class Owner
        {
            public const string PhotoLimitReached = "OWNER_PHOTO_LIMIT_REACHED";          // {0} en fazla
            public const string PhotoNotFound = "OWNER_PHOTO_NOT_FOUND";
            public const string PhotoRequired = "OWNER_PHOTO_REQUIRED";
            public const string LastPhotoCannotDelete = "OWNER_LAST_PHOTO_CANNOT_DELETE";
            public const string PitchHasReservations = "OWNER_PITCH_HAS_RESERVATIONS";
            public const string PitchNotInVenue = "OWNER_PITCH_NOT_IN_VENUE";
            public const string PriceRuleNotFound = "OWNER_PRICE_RULE_NOT_FOUND";
            public const string CampaignNotFound = "OWNER_CAMPAIGN_NOT_FOUND";
            public const string BlockNotFound = "OWNER_BLOCK_NOT_FOUND";
            public const string BlockReservationConflict = "OWNER_BLOCK_RESERVATION_CONFLICT";
            public const string BlockManualConflict = "OWNER_BLOCK_MANUAL_CONFLICT";
            public const string BlockClosedConflict = "OWNER_BLOCK_CLOSED_CONFLICT";
            public const string BlockHasManual = "OWNER_BLOCK_HAS_MANUAL";
            public const string BlockPaymentOnlyManual = "OWNER_BLOCK_PAYMENT_ONLY_MANUAL";
            public const string ReviewNotFound = "OWNER_REVIEW_NOT_FOUND";
            public const string ExpenseNotFound = "OWNER_EXPENSE_NOT_FOUND";
        }

        /// <summary>Admin: işletme hesapları ve tesisler.</summary>
        public static partial class Admin
        {
            public const string OwnerNotFound = "ADMIN_OWNER_NOT_FOUND";
            public const string UserNameTaken = "ADMIN_USERNAME_TAKEN";
            public const string CreateOwnerFailed = "ADMIN_CREATE_OWNER_FAILED";
            public const string NotAnOwner = "ADMIN_NOT_AN_OWNER";
            public const string InvalidOwnerModel = "ADMIN_INVALID_OWNER_MODEL";
            public const string PhotoUploadFailed = "ADMIN_PHOTO_UPLOAD_FAILED";          // {0} dosya adı, {1} hata mesajı
        }

        /// <summary>OpenStreetMap içe aktarma.</summary>
        public static partial class Osm
        {
            public const string InvalidBounds = "OSM_INVALID_BOUNDS";
            public const string CityNotFound = "OSM_CITY_NOT_FOUND";
            public const string AreaRequired = "OSM_AREA_REQUIRED";
            public const string InvalidResponse = "OSM_INVALID_RESPONSE";
            public const string AllMirrorsFailed = "OSM_ALL_MIRRORS_FAILED";
            public const string ServiceError = "OSM_SERVICE_ERROR";                       // {0} HTTP durum kodu
            public const string Unreachable = "OSM_UNREACHABLE";
            public const string AlreadyImported = "OSM_ALREADY_IMPORTED";
        }
    }
}
