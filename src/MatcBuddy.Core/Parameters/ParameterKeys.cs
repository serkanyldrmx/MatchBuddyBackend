namespace MatcBuddy.Core.Parameters
{
    /// <summary>
    /// Sistem parametresi anahtarları (SystemParameters tablosu). Varsayılanlar/aralıklar: ParameterCatalog.
    /// Tam liste: docs/platform-api.md.
    /// </summary>
    public static class ParameterKeys
    {
        public static class Media
        {
            public const string MaxUploadMb = "media.maxUploadMb";
            public const string MaxMegapixels = "media.maxMegapixels";
            public const string PendingCleanupHours = "media.pendingCleanupHours";
            public const string DeletedPurgeDays = "media.deletedPurgeDays";
            public const string CleanupBatchSize = "media.cleanupBatchSize";
        }

        public static class RateLimit
        {
            public const string AuthPerMinute = "rateLimit.authPerMinute";
            public const string UploadPerMinute = "rateLimit.uploadPerMinute";
        }

        public static class Venue
        {
            public const string MaxPhotos = "venue.maxPhotos";
            public const string MaxPrice = "venue.maxPrice";
            public const string AllowedDurations = "venue.allowedDurations";
            public const string SlotStepMinutes = "venue.slotStepMinutes";
            public const string PlayersPerSideMin = "venue.playersPerSideMin";
            public const string PlayersPerSideMax = "venue.playersPerSideMax";
            public const string ExtraPlayersAllowed = "venue.extraPlayersAllowed";
            public const string BookingMinPlayersPerSide = "venue.bookingMinPlayersPerSide";
            public const string CalendarMaxDays = "venue.calendarMaxDays";
            public const string BlockMaxDays = "venue.blockMaxDays";
            public const string MaxDiscountPercent = "venue.maxDiscountPercent";
            public const string ReviewsShown = "venue.reviewsShown";
            public const string CreateOwnerMaxPhotos = "venue.createOwnerMaxPhotos";
            public const string DefaultMembershipDays = "venue.defaultMembershipDays";
            public const string DealMaxPerDay = "venue.dealMaxPerDay";
            public const string DealMaxDaysAhead = "venue.dealMaxDaysAhead";
            public const string DealAudienceDays = "venue.dealAudienceDays";
            public const string DealMaxRecipients = "venue.dealMaxRecipients";
            public const string RepeatMaxWeeks = "venue.repeatMaxWeeks";
            public const string SeriesLeadDays = "venue.seriesLeadDays";
        }

        public static class Booking
        {
            public const string DefaultMinNoticeHours = "booking.defaultMinNoticeHours";
            public const string DefaultFreeCancellationHours = "booking.defaultFreeCancellationHours";
            public const string DefaultMaxAdvanceDays = "booking.defaultMaxAdvanceDays";
        }

        public static class Match
        {
            public const string ReminderLeadHours = "match.reminderLeadHours";
            public const string MaxScore = "match.maxScore";
            public const string MaxPlayerStat = "match.maxPlayerStat";
            public const string RatingMax = "match.ratingMax";
            public const string LeaderboardMax = "match.leaderboardMax";
        }

        public static class Social
        {
            public const string MaxInviteBatch = "social.maxInviteBatch";
            public const string FeedMaxTake = "social.feedMaxTake";
        }

        public static class Tournament
        {
            public const string MinTeams = "tournament.minTeams";
            public const string MaxTeams = "tournament.maxTeams";
            public const string LeagueMinTeamsToStart = "tournament.leagueMinTeamsToStart";
            public const string KnockoutMinTeamsToStart = "tournament.knockoutMinTeamsToStart";
            public const string RoundIntervalDays = "tournament.roundIntervalDays";
            public const string PointsWin = "tournament.pointsWin";
            public const string PointsDraw = "tournament.pointsDraw";
            public const string MaxScore = "tournament.maxScore";
        }

        public static class Osm
        {
            public const string MaxResults = "osm.maxResults";
            public const string DuplicateRadiusMeters = "osm.duplicateRadiusMeters";
            public const string MaxBoxDegrees = "osm.maxBoxDegrees";
            public const string OverpassTimeoutSeconds = "osm.overpassTimeoutSeconds";
        }

        public static class Security
        {
            public const string PasswordMinLength = "security.passwordMinLength";
            public const string TempPasswordLength = "security.tempPasswordLength";
        }

        /// <summary>Oturum, tek kullanımlık kod ve sosyal giriş ayarları (docs/auth-api.md).</summary>
        public static class Auth
        {
            public const string AccessTokenMinutes = "auth.accessTokenMinutes";
            public const string LegacyAccessTokenMinutes = "auth.legacyAccessTokenMinutes";
            public const string RefreshTokenDays = "auth.refreshTokenDays";
            public const string SessionRefreshTokenHours = "auth.sessionRefreshTokenHours";
            public const string RefreshReuseGraceSeconds = "auth.refreshReuseGraceSeconds";
            public const string CodeTtlMinutes = "auth.codeTtlMinutes";
            public const string CodeMaxAttempts = "auth.codeMaxAttempts";
            public const string CodeResendCooldownSeconds = "auth.codeResendCooldownSeconds";
            public const string EmailVerificationTtlMinutes = "auth.emailVerificationTtlMinutes";
            public const string PasswordResetPerMinute = "auth.passwordResetPerMinute";
            public const string PasswordResetPerIdentifierPerHour = "auth.passwordResetPerIdentifierPerHour";
            public const string PasswordRequireLetterAndDigit = "auth.passwordRequireLetterAndDigit";
            public const string FacebookEmailTrusted = "auth.facebookEmailTrusted";
        }

        /// <summary>Güvenilirlik puanı (docs/reliability-api.md).</summary>
        public static class Reliability
        {
            public const string NewPlayerMatchCount = "reliability.newPlayerMatchCount";
            public const string LevelGood = "reliability.levelGood";
            public const string LevelMedium = "reliability.levelMedium";
            public const string AttendBonus = "reliability.attendBonus";
            public const string LateCancelHours = "reliability.lateCancelHours";
            public const string LateCancelPenalty = "reliability.lateCancelPenalty";
            public const string VeryLateCancelHours = "reliability.veryLateCancelHours";
            public const string VeryLateCancelPenalty = "reliability.veryLateCancelPenalty";
            public const string NoShowPenalty = "reliability.noShowPenalty";
            public const string NoShowMarkWindowHours = "reliability.noShowMarkWindowHours";
            public const string OrganizerLateCancelPenalty = "reliability.organizerLateCancelPenalty";
            public const string SubstituteCandidateLimit = "reliability.substituteCandidateLimit";
            public const string SubstituteLookbackDays = "reliability.substituteLookbackDays";
        }

        /// <summary>KVKK / kullanım koşulları.</summary>
        public static class Legal
        {
            public const string TermsVersion = "legal.termsVersion";
        }
    }
}
