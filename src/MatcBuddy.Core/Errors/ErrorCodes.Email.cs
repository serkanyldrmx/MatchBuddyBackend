namespace MatcBuddy.Core.Errors
{
    public static partial class ErrorCodes
    {
        public static partial class Email
        {
            public const string TemplateNotFound = "EMAIL_TEMPLATE_NOT_FOUND";
            public const string SystemTemplate = "EMAIL_SYSTEM_TEMPLATE";
            public const string CampaignNotFound = "EMAIL_CAMPAIGN_NOT_FOUND";
            public const string CampaignLocked = "EMAIL_CAMPAIGN_LOCKED";
            public const string InvalidUrl = "EMAIL_INVALID_URL";
            public const string InvalidSchedule = "EMAIL_INVALID_SCHEDULE";
            public const string OwnerTooFrequent = "EMAIL_OWNER_TOO_FREQUENT";     // {0} gün
            public const string NoEmail = "EMAIL_NO_ADDRESS";
            public const string NotInMatch = "EMAIL_NOT_IN_MATCH";
        }
    }
}
