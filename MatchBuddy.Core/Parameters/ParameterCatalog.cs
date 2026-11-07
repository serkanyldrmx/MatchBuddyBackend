using System.Globalization;

namespace MatchBuddy.Core.Parameters
{
    /// <summary>Parametre veri türü (SystemParameters.DataType).</summary>
    public enum ParameterDataType : byte
    {
        Int = 1,
        Decimal = 2,
        Bool = 3,
        String = 4,
        Json = 5,
    }

    /// <summary>Koddaki parametre tanımı: tablo bu listeyle (eksik anahtarlar eklenerek) senkronlanır.</summary>
    public sealed record ParameterDefinition(
        string Key,
        ParameterDataType DataType,
        string DefaultValue,
        string Group,
        bool IsPublic = false,
        string? MinValue = null,
        string? MaxValue = null)
    {
        /// <summary>Türkçe açıklama Messages.resx'te: PARAMDESC_ + anahtar (nokta → alt çizgi, büyük harf).</summary>
        public string DescriptionKey => DescriptionKeyOf(Key);

        public static string DescriptionKeyOf(string key) => "PARAMDESC_" + key.Replace('.', '_').ToUpperInvariant();
    }

    /// <summary>
    /// Tüm sistem parametreleri ve varsayılanları (eski sabit değerler). Uygulama açılışında tabloda olmayanlar
    /// bu varsayılanlarla eklenir; var olan değerlere dokunulmaz.
    /// </summary>
    public static class ParameterCatalog
    {
        public const string GroupMedia = "Media";
        public const string GroupRateLimit = "RateLimit";
        public const string GroupVenue = "Venue";
        public const string GroupBooking = "Booking";
        public const string GroupMatch = "Match";
        public const string GroupSocial = "Social";
        public const string GroupTournament = "Tournament";
        public const string GroupOsm = "Osm";
        public const string GroupSecurity = "Security";
        public const string GroupAuth = "Auth";
        public const string GroupLegal = "Legal";
        public const string GroupReliability = "Reliability";

        private static string S(int v) => v.ToString(CultureInfo.InvariantCulture);

        private static ParameterDefinition Int(string key, int def, string group, int min, int max, bool isPublic = false) =>
            new(key, ParameterDataType.Int, S(def), group, isPublic, S(min), S(max));

        public static readonly IReadOnlyList<ParameterDefinition> All = new List<ParameterDefinition>
        {
            // Medya
            Int(ParameterKeys.Media.MaxUploadMb, 10, GroupMedia, 1, 20, isPublic: true),
            Int(ParameterKeys.Media.MaxMegapixels, 40, GroupMedia, 1, 100, isPublic: true),
            Int(ParameterKeys.Media.PendingCleanupHours, 24, GroupMedia, 1, 720),
            Int(ParameterKeys.Media.DeletedPurgeDays, 7, GroupMedia, 1, 365),
            Int(ParameterKeys.Media.CleanupBatchSize, 500, GroupMedia, 10, 10000),

            // İstek sınırları (dakika başına)
            Int(ParameterKeys.RateLimit.AuthPerMinute, 10, GroupRateLimit, 1, 1000),
            Int(ParameterKeys.RateLimit.UploadPerMinute, 20, GroupRateLimit, 1, 1000),

            // Tesis
            Int(ParameterKeys.Venue.MaxPhotos, 20, GroupVenue, 1, 100, isPublic: true),
            new(ParameterKeys.Venue.MaxPrice, ParameterDataType.Decimal, "100000", GroupVenue, true, "1", "10000000"),
            new(ParameterKeys.Venue.AllowedDurations, ParameterDataType.Json, "[60,90,120]", GroupVenue, true),
            Int(ParameterKeys.Venue.SlotStepMinutes, 30, GroupVenue, 5, 60, isPublic: true),
            Int(ParameterKeys.Venue.PlayersPerSideMin, 5, GroupVenue, 1, 11, isPublic: true),
            Int(ParameterKeys.Venue.PlayersPerSideMax, 11, GroupVenue, 1, 15, isPublic: true),
            Int(ParameterKeys.Venue.ExtraPlayersAllowed, 4, GroupVenue, 0, 20, isPublic: true),
            Int(ParameterKeys.Venue.BookingMinPlayersPerSide, 4, GroupVenue, 1, 11, isPublic: true),
            Int(ParameterKeys.Venue.CalendarMaxDays, 62, GroupVenue, 7, 366),
            Int(ParameterKeys.Venue.BlockMaxDays, 31, GroupVenue, 1, 366),
            Int(ParameterKeys.Venue.MaxDiscountPercent, 90, GroupVenue, 1, 100),
            Int(ParameterKeys.Venue.ReviewsShown, 20, GroupVenue, 1, 200),
            Int(ParameterKeys.Venue.CreateOwnerMaxPhotos, 10, GroupVenue, 1, 20),
            Int(ParameterKeys.Venue.DefaultMembershipDays, 0, GroupVenue, 0, 3650),

            // Rezervasyon: tesis ayarlarının (VenueSettings) sistem varsayılanları
            Int(ParameterKeys.Booking.DefaultMinNoticeHours, 0, GroupBooking, 0, 168, isPublic: true),
            Int(ParameterKeys.Booking.DefaultFreeCancellationHours, 24, GroupBooking, 0, 720, isPublic: true),
            Int(ParameterKeys.Booking.DefaultMaxAdvanceDays, 90, GroupBooking, 1, 365, isPublic: true),

            // Maç
            Int(ParameterKeys.Match.ReminderLeadHours, 24, GroupMatch, 1, 168),
            Int(ParameterKeys.Match.MaxScore, 50, GroupMatch, 1, 999, isPublic: true),
            Int(ParameterKeys.Match.MaxPlayerStat, 30, GroupMatch, 1, 999, isPublic: true),
            Int(ParameterKeys.Match.RatingMax, 5, GroupMatch, 2, 10, isPublic: true),
            Int(ParameterKeys.Match.LeaderboardMax, 100, GroupMatch, 1, 1000),

            // Sosyal
            Int(ParameterKeys.Social.MaxInviteBatch, 50, GroupSocial, 1, 500),
            Int(ParameterKeys.Social.FeedMaxTake, 100, GroupSocial, 1, 500),

            // Turnuva
            Int(ParameterKeys.Tournament.MinTeams, 4, GroupTournament, 2, 64, isPublic: true),
            Int(ParameterKeys.Tournament.MaxTeams, 16, GroupTournament, 2, 128, isPublic: true),
            Int(ParameterKeys.Tournament.LeagueMinTeamsToStart, 3, GroupTournament, 2, 64),
            Int(ParameterKeys.Tournament.KnockoutMinTeamsToStart, 2, GroupTournament, 2, 64),
            Int(ParameterKeys.Tournament.RoundIntervalDays, 7, GroupTournament, 1, 60),
            Int(ParameterKeys.Tournament.PointsWin, 3, GroupTournament, 1, 10),
            Int(ParameterKeys.Tournament.PointsDraw, 1, GroupTournament, 0, 10),
            Int(ParameterKeys.Tournament.MaxScore, 50, GroupTournament, 1, 999),

            // OpenStreetMap (yönetici aday arama)
            Int(ParameterKeys.Osm.MaxResults, 200, GroupOsm, 1, 1000),
            Int(ParameterKeys.Osm.DuplicateRadiusMeters, 150, GroupOsm, 10, 5000),
            new(ParameterKeys.Osm.MaxBoxDegrees, ParameterDataType.Decimal, "2", GroupOsm, false, "0.1", "5"),
            Int(ParameterKeys.Osm.OverpassTimeoutSeconds, 25, GroupOsm, 5, 180),

            // Güvenlik (iş kuralı düzeyi; JWT anahtarı/CORS yapılandırmada kalır)
            Int(ParameterKeys.Security.PasswordMinLength, 6, GroupSecurity, 6, 64, isPublic: true),
            Int(ParameterKeys.Security.TempPasswordLength, 12, GroupSecurity, 8, 64),

            // Kimlik doğrulama (docs/auth-api.md). Gizli anahtarlar (JWT, Google, Facebook, SMTP, Netgsm) yapılandırmada kalır.
            Int(ParameterKeys.Auth.AccessTokenMinutes, 15, GroupAuth, 1, 1440, isPublic: true),
            Int(ParameterKeys.Auth.LegacyAccessTokenMinutes, 120, GroupAuth, 5, 1440),
            Int(ParameterKeys.Auth.RefreshTokenDays, 30, GroupAuth, 1, 365),
            Int(ParameterKeys.Auth.SessionRefreshTokenHours, 24, GroupAuth, 1, 720),
            Int(ParameterKeys.Auth.RefreshReuseGraceSeconds, 15, GroupAuth, 0, 120),
            Int(ParameterKeys.Auth.CodeTtlMinutes, 15, GroupAuth, 1, 1440, isPublic: true),
            Int(ParameterKeys.Auth.CodeMaxAttempts, 5, GroupAuth, 1, 20),
            Int(ParameterKeys.Auth.CodeResendCooldownSeconds, 60, GroupAuth, 0, 3600, isPublic: true),
            Int(ParameterKeys.Auth.EmailVerificationTtlMinutes, 1440, GroupAuth, 5, 10080),
            Int(ParameterKeys.Auth.PasswordResetPerMinute, 5, GroupAuth, 1, 1000),
            Int(ParameterKeys.Auth.PasswordResetPerIdentifierPerHour, 5, GroupAuth, 1, 1000),
            Bool(ParameterKeys.Auth.PasswordRequireLetterAndDigit, false, GroupAuth, isPublic: true),
            Bool(ParameterKeys.Auth.FacebookEmailTrusted, false, GroupAuth),

            // Güvenilirlik puanı (docs/reliability-api.md). Hepsi herkese açık: arayüz ceza önizlemesi ve rozet seviyesi için kullanır.
            Int(ParameterKeys.Reliability.NewPlayerMatchCount, 3, GroupReliability, 0, 50, isPublic: true),
            Int(ParameterKeys.Reliability.LevelGood, 90, GroupReliability, 1, 100, isPublic: true),
            Int(ParameterKeys.Reliability.LevelMedium, 70, GroupReliability, 0, 100, isPublic: true),
            Int(ParameterKeys.Reliability.AttendBonus, 2, GroupReliability, 0, 50, isPublic: true),
            Int(ParameterKeys.Reliability.LateCancelHours, 24, GroupReliability, 1, 168, isPublic: true),
            Int(ParameterKeys.Reliability.LateCancelPenalty, 5, GroupReliability, 0, 100, isPublic: true),
            Int(ParameterKeys.Reliability.VeryLateCancelHours, 3, GroupReliability, 0, 72, isPublic: true),
            Int(ParameterKeys.Reliability.VeryLateCancelPenalty, 10, GroupReliability, 0, 100, isPublic: true),
            Int(ParameterKeys.Reliability.NoShowPenalty, 20, GroupReliability, 0, 100, isPublic: true),
            Int(ParameterKeys.Reliability.NoShowMarkWindowHours, 48, GroupReliability, 1, 336, isPublic: true),
            Int(ParameterKeys.Reliability.OrganizerLateCancelPenalty, 10, GroupReliability, 0, 100, isPublic: true),

            // KVKK: kayıt / sosyal girişte kabul edilmesi gereken kullanım koşulları + aydınlatma metni sürümü
            new(ParameterKeys.Legal.TermsVersion, ParameterDataType.String, "2026-11-06", GroupLegal, true),
        };

        private static ParameterDefinition Bool(string key, bool def, string group, bool isPublic = false) =>
            new(key, ParameterDataType.Bool, def ? "true" : "false", group, isPublic);

        private static readonly Dictionary<string, ParameterDefinition> ByKey =
            All.ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);

        public static ParameterDefinition? Find(string key) => ByKey.TryGetValue(key, out var d) ? d : null;
    }
}
