namespace MatcBuddy.Core.Errors
{
    public static partial class ErrorCodes
    {
        /// <summary>Harici (anlaşmasız) sahada maç (docs/external-venues-api.md).</summary>
        public static partial class External
        {
            public const string VenueNameRequired = "EXTERNAL_VENUE_NAME_REQUIRED";   // {0} en az, {1} en fazla
            public const string VenueNotFound = "EXTERNAL_VENUE_NOT_FOUND";
            public const string DateRequired = "EXTERNAL_DATE_REQUIRED";
            public const string PastDate = "EXTERNAL_PAST_DATE";
            public const string TooFarAhead = "EXTERNAL_TOO_FAR_AHEAD";               // {0} gün
            public const string InvalidPrice = "EXTERNAL_INVALID_PRICE";              // {0} en fazla
            public const string NotExternal = "EXTERNAL_NOT_EXTERNAL_MATCH";
            public const string MatchClosed = "EXTERNAL_MATCH_CLOSED";
        }
    }
}
