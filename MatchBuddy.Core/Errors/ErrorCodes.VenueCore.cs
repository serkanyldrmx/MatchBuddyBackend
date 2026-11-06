namespace MatchBuddy.Core.Errors
{
    public static partial class ErrorCodes
    {
        /// <summary>Anlaşmalı tesis (venue) hata kodları: doğrulama, müsaitlik, işletme, gider.</summary>
        public static partial class Venue
        {
            // Genel / alan bazlı (etiket {0} = MessageKeys.Venue.Label*)
            public const string FieldMustBeEntered = "VENUE_FIELD_MUST_BE_ENTERED";     // {0} alan
            public const string FieldRequired = "VENUE_FIELD_REQUIRED";                 // {0} alan
            public const string PriceOutOfRange = "VENUE_PRICE_OUT_OF_RANGE";           // {0} alan, {1} en fazla

            // Oyuncu / rezervasyon
            public const string PitchRequired = "VENUE_PITCH_REQUIRED";
            public const string ChooseSlot = "VENUE_CHOOSE_SLOT";
            public const string ReviewEmpty = "VENUE_REVIEW_EMPTY";
            public const string InvalidDuration = "VENUE_INVALID_DURATION";
            public const string MatchAndTeamRequired = "VENUE_MATCH_AND_TEAM_REQUIRED";
            public const string ReservationRequired = "VENUE_RESERVATION_REQUIRED";
            public const string SlotTaken = "VENUE_SLOT_TAKEN";
            public const string MembershipInactive = "VENUE_MEMBERSHIP_INACTIVE";
            public const string InvalidStartMinute = "VENUE_INVALID_START_MINUTE";
            public const string PastSlot = "VENUE_PAST_SLOT";
            public const string ClosedAtTime = "VENUE_CLOSED_AT_TIME";
            public const string NoCleatRental = "VENUE_NO_CLEAT_RENTAL";
            public const string NoCamera = "VENUE_NO_CAMERA";

            // İşletme paneli
            public const string CalendarRangeTooLong = "VENUE_CALENDAR_RANGE_TOO_LONG"; // {0} gün
            public const string VenueInfoEmpty = "VENUE_INFO_EMPTY";
            public const string AddressEmpty = "VENUE_ADDRESS_EMPTY";
            public const string InvalidLocationFormat = "VENUE_INVALID_LOCATION_FORMAT";
            public const string PitchInfoEmpty = "VENUE_PITCH_INFO_EMPTY";
            public const string PitchNameEmpty = "VENUE_PITCH_NAME_EMPTY";
            public const string InvalidSurface = "VENUE_INVALID_SURFACE";
            public const string OpeningHoursSevenDays = "VENUE_OPENING_HOURS_SEVEN_DAYS";
            public const string OpeningHoursInvalidDays = "VENUE_OPENING_HOURS_INVALID_DAYS";
            public const string OpeningHoursInvalidTime = "VENUE_OPENING_HOURS_INVALID_TIME";
            public const string PriceRuleEmpty = "VENUE_PRICE_RULE_EMPTY";
            public const string InvalidDaysMask = "VENUE_INVALID_DAYS_MASK";
            public const string InvalidHourRange = "VENUE_INVALID_HOUR_RANGE";
            public const string CampaignEmpty = "VENUE_CAMPAIGN_EMPTY";
            public const string CampaignTitleEmpty = "VENUE_CAMPAIGN_TITLE_EMPTY";
            public const string StartEndDateRequired = "VENUE_START_END_DATE_REQUIRED";
            public const string EndDateBeforeStart = "VENUE_END_DATE_BEFORE_START";
            public const string DiscountOrFixedPrice = "VENUE_DISCOUNT_OR_FIXED_PRICE";
            public const string PitchMustBeSelected = "VENUE_PITCH_MUST_BE_SELECTED";
            public const string InvalidBlockKind = "VENUE_INVALID_BLOCK_KIND";
            public const string StartEndTimeRequired = "VENUE_START_END_TIME_REQUIRED";
            public const string EndTimeBeforeStart = "VENUE_END_TIME_BEFORE_START";
            public const string BlockTooLong = "VENUE_BLOCK_TOO_LONG";                  // {0} gün
            public const string StartTimeRequired = "VENUE_START_TIME_REQUIRED";
            public const string DurationOutOfRange = "VENUE_DURATION_OUT_OF_RANGE";     // {0} en az, {1} en fazla dakika
            public const string ReplyEmpty = "VENUE_REPLY_EMPTY";

            // Giderler / finans
            public const string ExpenseInfoEmpty = "VENUE_EXPENSE_INFO_EMPTY";
            public const string VenueMustBeSelected = "VENUE_MUST_BE_SELECTED";
            public const string InvalidDate = "VENUE_INVALID_DATE";
            public const string InvalidExpenseCategory = "VENUE_INVALID_EXPENSE_CATEGORY"; // {0} kategori listesi
            public const string AmountMustBePositive = "VENUE_AMOUNT_MUST_BE_POSITIVE";
            public const string AmountTooLarge = "VENUE_AMOUNT_TOO_LARGE";              // {0} en fazla
            public const string AmountTooManyDecimals = "VENUE_AMOUNT_TOO_MANY_DECIMALS";
            public const string ExpenseMustBeSelected = "VENUE_EXPENSE_MUST_BE_SELECTED";
            public const string InvalidYear = "VENUE_INVALID_YEAR";

            // Admin
            public const string OwnerNotFound = "VENUE_OWNER_NOT_FOUND";
            public const string OwnerInfoRequired = "VENUE_OWNER_INFO_REQUIRED";
            public const string VenueInfoRequired = "VENUE_INFO_REQUIRED";
            public const string UserNameTaken = "VENUE_USERNAME_TAKEN";
            public const string LocationRequired = "VENUE_LOCATION_REQUIRED";
            public const string PhotoRequired = "VENUE_PHOTO_REQUIRED";                 // {0} en az
            public const string PhotoLimitExceeded = "VENUE_PHOTO_LIMIT_EXCEEDED";      // {0} en fazla
            public const string OwnerMustBeSelected = "VENUE_OWNER_MUST_BE_SELECTED";
            public const string VenueAndOwnerRequired = "VENUE_AND_OWNER_REQUIRED";
            public const string CandidateEmpty = "VENUE_CANDIDATE_EMPTY";
            public const string InvalidLocation = "VENUE_INVALID_LOCATION";

            // Eski stadyum modeli (JSON dönüştürücü)
            public const string NumberOrStringExpected = "VENUE_NUMBER_OR_STRING_EXPECTED";
        }
    }
}
