namespace MatcBuddy.Core.Errors
{
    public static partial class ErrorCodes
    {
        /// <summary>İşletme büyüme araçları: boş saat fırsatı, haftalık tekrar (docs/venue-growth-api.md).</summary>
        public static partial class Owner
        {
            public const string DealLimit = "OWNER_DEAL_LIMIT";                     // {0} günlük en fazla
            public const string DealNotFuture = "OWNER_DEAL_NOT_FUTURE";
            public const string DealTooFar = "OWNER_DEAL_TOO_FAR";                  // {0} gün
            public const string DealCrossesMidnight = "OWNER_DEAL_CROSSES_MIDNIGHT";
            public const string RepeatOnlyNewManual = "OWNER_REPEAT_ONLY_NEW_MANUAL";
        }

        public static partial class Match
        {
            public const string NotRepeating = "MATCH_NOT_REPEATING";
        }
    }
}
