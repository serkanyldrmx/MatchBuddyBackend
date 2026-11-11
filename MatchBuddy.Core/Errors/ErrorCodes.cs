namespace MatchBuddy.Core.Errors
{
    /// <summary>
    /// Merkezi hata kodu kataloğu. Her kodun Türkçe metni Resources/Messages.resx'te aynı anahtarla durur.
    /// Alanlara göre gruplanmıştır; alan sınıfları partial'dır (ErrorCodes.*.cs dosyalarında devam eder).
    /// Tam liste: docs/platform-api.md.
    /// </summary>
    public static partial class ErrorCodes
    {
        public static partial class Common
        {
            public const string BadRequest = "COMMON_BAD_REQUEST";
            public const string NotFound = "COMMON_NOT_FOUND";
            public const string MethodNotAllowed = "COMMON_METHOD_NOT_ALLOWED";
            public const string UnsupportedMediaType = "COMMON_UNSUPPORTED_MEDIA_TYPE";
            public const string PayloadTooLarge = "COMMON_PAYLOAD_TOO_LARGE";
            public const string Conflict = "COMMON_CONFLICT";
            public const string RateLimited = "COMMON_RATE_LIMITED";
            public const string InternalError = "COMMON_INTERNAL_ERROR";
            public const string ServiceUnavailable = "COMMON_SERVICE_UNAVAILABLE";
            public const string UpstreamError = "COMMON_UPSTREAM_ERROR";
            public const string OperationFailed = "COMMON_OPERATION_FAILED";
            public const string InvalidJson = "COMMON_INVALID_JSON";
            public const string InvalidDateRange = "COMMON_INVALID_DATE_RANGE";
            public const string Maintenance = "COMMON_MAINTENANCE";
        }

        public static partial class Validation
        {
            /// <summary>Model doğrulaması (otomatik 400): errors sözlüğü ile döner.</summary>
            public const string Failed = "VALIDATION_FAILED";
            public const string Required = "VALIDATION_REQUIRED";              // {0} alan adı
            public const string TooLong = "VALIDATION_TOO_LONG";               // {0} alan, {1} en fazla
            public const string TooShort = "VALIDATION_TOO_SHORT";             // {0} alan, {1} en az
            public const string OutOfRange = "VALIDATION_OUT_OF_RANGE";        // {0} alan, {1} min, {2} max
            public const string InvalidValue = "VALIDATION_INVALID_VALUE";     // {0} değer, {1} alan
            public const string InvalidFormat = "VALIDATION_INVALID_FORMAT";
            public const string BodyRequired = "VALIDATION_BODY_REQUIRED";
            public const string MustBeNumber = "VALIDATION_MUST_BE_NUMBER";    // {0} alan
            public const string InvalidEmail = "VALIDATION_INVALID_EMAIL";
            public const string NegativeNotAllowed = "VALIDATION_NEGATIVE_NOT_ALLOWED"; // {0} alan
            public const string ValueMissing = "VALIDATION_VALUE_MISSING";
            public const string ValueInvalid = "VALIDATION_VALUE_INVALID";     // {0} değer
            public const string UnknownValueInvalid = "VALIDATION_UNKNOWN_VALUE_INVALID";
        }

        public static partial class Auth
        {
            public const string Unauthorized = "AUTH_UNAUTHORIZED";
            public const string Forbidden = "AUTH_FORBIDDEN";
            public const string InvalidCredentials = "AUTH_INVALID_CREDENTIALS";
            public const string AdminOnly = "AUTH_ADMIN_ONLY";
        }

        // Aşağıdaki alan sınıfları diğer dosyalarda genişletilir; ortak "bulunamadı" kodları burada.
        public static partial class Match
        {
            public const string NotFound = "MATCH_NOT_FOUND";
            public const string NameInvalid = "MATCH_NAME_INVALID";
            public const string TimeConflict = "MATCH_TIME_CONFLICT";
        }

        public static partial class Player
        {
            public const string NotFound = "PLAYER_NOT_FOUND";
            public const string NameInvalid = "PLAYER_NAME_INVALID";
        }

        public static partial class Team
        {
            public const string NotFound = "TEAM_NOT_FOUND";
            public const string NameInvalid = "TEAM_NAME_INVALID";
            public const string InTournament = "TEAM_IN_TOURNAMENT";
        }

        public static partial class Venue
        {
            public const string NotFound = "VENUE_NOT_FOUND";
            public const string PitchNotFound = "VENUE_PITCH_NOT_FOUND";
            public const string NotYourVenue = "VENUE_NOT_YOURS";
        }

        public static partial class Tournament
        {
            public const string NotFound = "TOURNAMENT_NOT_FOUND";
        }

        public static partial class Group
        {
            public const string NotFound = "GROUP_NOT_FOUND";
            public const string NameInvalid = "GROUP_NAME_INVALID";
            public const string NotMember = "GROUP_NOT_MEMBER";
            public const string CreatorOnly = "GROUP_CREATOR_ONLY";
            public const string NoPlayers = "GROUP_NO_PLAYERS";
        }

        public static partial class Social
        {
            public const string InvalidTheme = "SOCIAL_INVALID_THEME";         // {0} izinli değerler
            public const string InvalidLanguage = "SOCIAL_INVALID_LANGUAGE";   // {0} izinli değerler
        }
        public static partial class Media { }
        public static partial class Notification { }
        public static partial class Message { }
        public static partial class Invite { }
        public static partial class Comment { }

        public static partial class Parameter
        {
            public const string NotFound = "PARAM_NOT_FOUND";
            public const string InvalidType = "PARAM_INVALID_TYPE";          // {0} key, {1} beklenen tür
            public const string OutOfRange = "PARAM_OUT_OF_RANGE";           // {0} key, {1} min, {2} max
            public const string ValueRequired = "PARAM_VALUE_REQUIRED";
            public const string InvalidJson = "PARAM_INVALID_JSON";
            public const string InvalidList = "PARAM_INVALID_LIST";          // {0} key
        }

        public static partial class VenueSettings
        {
            public const string OutOfRange = "VENUE_SETTINGS_OUT_OF_RANGE";  // {0} alan, {1} min, {2} max
            public const string BookingTooSoon = "VENUE_BOOKING_TOO_SOON";   // {0} saat
            public const string BookingTooFarAhead = "VENUE_BOOKING_TOO_FAR_AHEAD"; // {0} gün
        }
    }
}
