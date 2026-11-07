namespace MatchBuddy.Entities.DTOs
{
    // Anlaşmalı tesis paketi DTO'ları (docs/venues-api.md). JSON camelCase.

    // ------------------------------------------------------------------ Oyuncu: /api/Venues

    public class VenueAmenitiesDto
    {
        public bool CleatRental { get; set; }
        public bool Camera { get; set; }
        public bool Shower { get; set; }
        public bool LockerRoom { get; set; }
        public bool Parking { get; set; }
        public bool Cafe { get; set; }
        public bool Shuttle { get; set; }
        public bool Bibs { get; set; }
        public bool Ball { get; set; }
    }

    public class VenueListItemDto
    {
        public int StadiumId { get; set; }
        public string? Name { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? Address { get; set; }
        public string? Location { get; set; }
        public string? CoverPhotoUrl { get; set; }
        public int PitchCount { get; set; }
        public VenueAmenitiesDto Amenities { get; set; } = new();
        public decimal? MinPrice { get; set; }
        public bool HasActiveCampaign { get; set; }
        public double? Rating { get; set; }
        public int ReviewCount { get; set; }
        public int FreeSlotsToday { get; set; }
    }

    public class VenuePhotoDto
    {
        public int PhotoId { get; set; }
        public string Url { get; set; } = string.Empty;
        public bool IsCover { get; set; }
        public int SortOrder { get; set; }
    }

    public class PitchDto
    {
        public int PitchId { get; set; }
        public int StadiumId { get; set; }
        public string Name { get; set; } = string.Empty;
        public byte Surface { get; set; }
        public bool IsIndoor { get; set; }
        public int PlayersPerSide { get; set; }
        public decimal BasePrice { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>openTime / closeTime "HH:mm"; closeTime "00:00" = gece yarısı.</summary>
    public class OpeningHourDto
    {
        public int DayOfWeek { get; set; }
        public bool IsClosed { get; set; }
        public string? OpenTime { get; set; }
        public string? CloseTime { get; set; }
    }

    public class VenuePricesDto
    {
        public decimal? CleatRentalPrice { get; set; }
        public decimal? CameraPrice { get; set; }
    }

    /// <summary>startDate / endDate "yyyy-MM-dd".</summary>
    public class CampaignDto
    {
        public int CampaignId { get; set; }
        public int StadiumId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public int DaysMask { get; set; }
        public int FromHour { get; set; }
        public int ToHour { get; set; }
        public int? DiscountPercent { get; set; }
        public decimal? FixedPrice { get; set; }
        public int? PitchId { get; set; }
        public bool IsActive { get; set; }
        /// <summary>Boş saat fırsatı (tek saat, bildirimli).</summary>
        public bool IsDeal { get; set; }
    }

    public class VenueOwnerRefDto
    {
        public int PlayerId { get; set; }
        public string? Name { get; set; }
    }

    public class VenueReviewDto
    {
        public int ReviewId { get; set; }
        public int MatchId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public PlayerSummaryDto? Player { get; set; }
        public string? OwnerReply { get; set; }
        public DateTime? RepliedAt { get; set; }
        public ReviewMatchRefDto? Match { get; set; }
    }

    public class ReviewMatchRefDto
    {
        public int MatchId { get; set; }
        public string? MatchName { get; set; }
        public DateTime? MatchDate { get; set; }
    }

    public class VenueDetailDto : VenueListItemDto
    {
        public string? Description { get; set; }
        public string? Phone { get; set; }
        public string? CancellationPolicy { get; set; }
        public List<VenuePhotoDto> Photos { get; set; } = new();
        public List<PitchDto> Pitches { get; set; } = new();
        public List<OpeningHourDto> OpeningHours { get; set; } = new();
        public VenuePricesDto Prices { get; set; } = new();
        public List<CampaignDto> Campaigns { get; set; } = new();
        public VenueOwnerRefDto? Owner { get; set; }
        public List<VenueReviewDto> Reviews { get; set; } = new();
        public Dictionary<string, int> RatingBreakdown { get; set; } = new();
    }

    /// <summary>GET /api/Owner/Venue: Detail şekli + yayın durumu ve eksikler (pasif sahalar dahil).</summary>
    public class OwnerVenueDetailDto : VenueDetailDto
    {
        public bool IsListed { get; set; }
        public bool IsVisible { get; set; }
        public List<string> Missing { get; set; } = new();
    }

    public class SlotDto
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Status { get; set; } = "free"; // free | booked | pending | blocked | past
        public decimal Price { get; set; }
        public decimal OriginalPrice { get; set; }
        public string? CampaignTitle { get; set; }
    }

    public class PitchSlotsDto
    {
        public int PitchId { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<SlotDto> Slots { get; set; } = new();
    }

    public class AvailabilityDto
    {
        public string Date { get; set; } = string.Empty; // yyyy-MM-dd
        public bool IsClosed { get; set; }
        public List<PitchSlotsDto> Pitches { get; set; } = new();
    }

    public class QuoteInput
    {
        public int PitchId { get; set; }
        public DateTime? Start { get; set; }
        public int DurationMinutes { get; set; } = 60;
        public int UserCount { get; set; }
        public int CleatCount { get; set; }
        public bool CameraRequested { get; set; }
    }

    public class QuoteLineDto
    {
        public string Label { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal OriginalPrice { get; set; }
        public string? CampaignTitle { get; set; }
    }

    public class QuoteDto
    {
        public bool Available { get; set; }
        public string? Reason { get; set; }
        public List<QuoteLineDto> Lines { get; set; } = new();
        public decimal PitchTotal { get; set; }
        public decimal ExtrasTotal { get; set; }
        public decimal Total { get; set; }
        /// <summary>Kişi başı pay (krampon hariç).</summary>
        public decimal PerPlayer { get; set; }
        /// <summary>Krampon toplamı (yalnızca kiralayanlar öder, kişi başına bölünmez).</summary>
        public decimal CleatTotal { get; set; }
        /// <summary>Bir çift krampon ücreti (tesis kiralamıyorsa null).</summary>
        public decimal? CleatPrice { get; set; }
    }

    public class ReviewInput
    {
        public int MatchId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
    }

    // ------------------------------------------------------------------ Maç kurma / maç DTO eklentileri

    public class SaveMatchInput
    {
        public string? MatchName { get; set; }
        public string? Description { get; set; }
        public int UserCount { get; set; }
        public int? PitchId { get; set; }
        public DateTime? MatchDate { get; set; }
        public int? DurationMinutes { get; set; }
        public int CleatCount { get; set; }
        public bool CameraRequested { get; set; }
        public int? HomeTeamId { get; set; }
        public int? AwayTeamId { get; set; }
        public bool LookingForOpponent { get; set; }
        /// <summary>En düşük güvenilirlik puanı (null = sınır yok) — docs/reliability-api.md</summary>
        public int? MinReliability { get; set; }
        /// <summary>Her hafta aynı gün ve saatte tekrar et (docs/venue-growth-api.md).</summary>
        public bool RepeatWeekly { get; set; }
    }

    public class MatchVenueDto
    {
        public int StadiumId { get; set; }
        public string? Name { get; set; }
        public int? PitchId { get; set; }
        public string? PitchName { get; set; }
        public bool IsIndoor { get; set; }
        public byte Surface { get; set; }
        public string? CoverPhotoUrl { get; set; }
        public string? CancellationPolicy { get; set; }
    }

    public class MyReviewDto
    {
        public int Rating { get; set; }
        public string? Comment { get; set; }
    }

    public class ClaimOpponentInput
    {
        public int MatchId { get; set; }
        public int TeamId { get; set; }
    }

    // ------------------------------------------------------------------ İşletme: /api/Owner

    public class OwnerVenueDto
    {
        public int StadiumId { get; set; }
        public string? Name { get; set; }
        public string? CoverPhotoUrl { get; set; }
        public int PitchCount { get; set; }
        public bool IsVisible { get; set; }
        public List<string> Missing { get; set; } = new();
    }

    public class OwnerVenuesDto
    {
        public DateTime? MembershipUntil { get; set; }
        public bool MembershipActive { get; set; }
        public bool MembershipSuspended { get; set; }
        public List<OwnerVenueDto> Venues { get; set; } = new();
    }

    public class PitchRefDto
    {
        public int PitchId { get; set; }
        public string? Name { get; set; }
    }

    /// <summary>İşletmenin gördüğü kurucu: herkese açık bilgiler + telefon (iletişim için).</summary>
    public class ReservationCreatorDto : PlayerSummaryDto
    {
        public string? PhoneNumber { get; set; }
    }

    public class ReservationDto
    {
        public int MatchId { get; set; }
        public int StadiumId { get; set; }
        public string? MatchName { get; set; }
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
        public int DurationMinutes { get; set; }
        public PitchRefDto? Pitch { get; set; }
        public byte Status { get; set; }
        public ReservationCreatorDto? Creator { get; set; }
        public int GoingCount { get; set; }
        public int Capacity { get; set; }
        public TeamRefDto? HomeTeam { get; set; }
        public TeamRefDto? AwayTeam { get; set; }
        public bool LookingForOpponent { get; set; }
        public int CleatCount { get; set; }
        public bool CameraRequested { get; set; }
        public decimal? PitchPrice { get; set; }
        public decimal? ExtrasPrice { get; set; }
        public decimal? TotalPrice { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? RejectReason { get; set; }
        /// <summary>Haftalık sabit maç serisi aktif mi.</summary>
        public bool RepeatWeekly { get; set; }
        /// <summary>Kurucunun güvenilirliği: işletme onaylamadan önce görür.</summary>
        public OrganizerReliabilityDto? CreatorReliability { get; set; }
    }

    public class OrganizerReliabilityDto
    {
        public int Score { get; set; }
        public string Level { get; set; } = "new";
        public int CompletedMatches { get; set; }
        /// <summary>Son 10 maçında son dakika çekilme / gelmeme.</summary>
        public int LateCancels { get; set; }
        public int NoShows { get; set; }
        /// <summary>Kurucu olarak son 180 günde: geçmiş (onaylı) rezervasyonları ve son dakika iptal ettikleri.</summary>
        public int PastReservations { get; set; }
        public int OrganizerLateCancels { get; set; }
    }

    // ------------------------------------------------------------------ Büyüme araçları (docs/venue-growth-api.md)

    public class SlotDealInput
    {
        public int PitchId { get; set; }
        public DateTime? Start { get; set; }
        public int DurationMinutes { get; set; }
        public int DiscountPercent { get; set; }
    }

    public class SlotDealResultDto
    {
        public int CampaignId { get; set; }
        public int Notified { get; set; }
    }

    public class SlotDealAudienceDto
    {
        public int Count { get; set; }
        public int SentToday { get; set; }
        public int DailyLimit { get; set; }
    }

    public class OwnerCustomerDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public int Bookings { get; set; }
        public DateTime LastAt { get; set; }
        public decimal? LastPrice { get; set; }
        public string? LastTitle { get; set; }
    }

    public class OwnerDashboardDto
    {
        public int TodayCount { get; set; }
        public int WeekCount { get; set; }
        public int PendingCount { get; set; }
        public int OccupancyPercent { get; set; }
        public decimal WeekRevenue { get; set; }
        public double? Rating { get; set; }
        public int ReviewCount { get; set; }
        public List<ReservationDto> Upcoming { get; set; } = new();
        public List<ReservationDto> Pending { get; set; } = new();
    }

    public class CalendarMatchItemDto
    {
        public string Kind { get; set; } = "match";
        public int MatchId { get; set; }
        public int PitchId { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public byte Status { get; set; }
        public string? Title { get; set; }
        public PlayerSummaryDto? Creator { get; set; }
        public int GoingCount { get; set; }
        public int Capacity { get; set; }
        public decimal? TotalPrice { get; set; }
    }

    public class CalendarBlockItemDto
    {
        // Öğe ayırıcı ("match" | "block"); bloğun türü (0 kapalı, 1 harici rezervasyon) BlockKind'da
        public string Kind { get; set; } = "block";
        public byte BlockKind { get; set; }
        public int SlotBlockId { get; set; }
        public int PitchId { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string? Reason { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public decimal? Price { get; set; }
        public bool IsPaid { get; set; }
        public string? Note { get; set; }
        public Guid? SeriesId { get; set; }
    }

    public class OwnerCalendarDto
    {
        public List<PitchRefDto> Pitches { get; set; } = new();
        public List<OpeningHourDto> OpeningHours { get; set; } = new();
        // CalendarMatchItemDto | CalendarBlockItemDto (kind alanı ile ayrılır)
        public List<object> Items { get; set; } = new();
    }

    public class RespondReservationInput
    {
        public int MatchId { get; set; }
        public bool Approve { get; set; }
        public string? Reason { get; set; }
    }

    public class VenueUpdateInput
    {
        public int StadiumId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? Location { get; set; }
        public string? CancellationPolicy { get; set; }
        public bool CleatRental { get; set; }
        public decimal? CleatRentalPrice { get; set; }
        public bool HasCamera { get; set; }
        public decimal? CameraPrice { get; set; }
        public bool HasShower { get; set; }
        public bool HasLockerRoom { get; set; }
        public bool HasParking { get; set; }
        public bool HasCafe { get; set; }
        public bool HasShuttle { get; set; }
        public bool ProvidesBibs { get; set; }
        public bool ProvidesBall { get; set; }
    }

    public class PhotoIdInput
    {
        public int PhotoId { get; set; }
    }

    public class PitchSaveInput
    {
        public int? PitchId { get; set; }
        public int StadiumId { get; set; }
        public string? Name { get; set; }
        public byte Surface { get; set; }
        public bool IsIndoor { get; set; }
        public int PlayersPerSide { get; set; }
        public decimal BasePrice { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class PitchIdInput
    {
        public int PitchId { get; set; }
    }

    public class OpeningHoursInput
    {
        public int StadiumId { get; set; }
        public List<OpeningHourDto>? Days { get; set; }
    }

    public class PriceRuleDto
    {
        public int? PriceRuleId { get; set; }
        public int StadiumId { get; set; }
        public int? PitchId { get; set; }
        public int DaysMask { get; set; }
        public int FromHour { get; set; }
        public int ToHour { get; set; }
        public decimal PricePerHour { get; set; }
    }

    public class PriceRuleIdInput
    {
        public int PriceRuleId { get; set; }
    }

    public class CampaignSaveInput
    {
        public int? CampaignId { get; set; }
        public int StadiumId { get; set; }
        public int? PitchId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int DaysMask { get; set; }
        public int FromHour { get; set; }
        public int ToHour { get; set; }
        public int? DiscountPercent { get; set; }
        public decimal? FixedPrice { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class CampaignIdInput
    {
        public int CampaignId { get; set; }
    }

    public class SlotBlockDto
    {
        public int SlotBlockId { get; set; }
        public int PitchId { get; set; }
        public string? PitchName { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        // 0 = kapalı/bakım, 1 = harici rezervasyon (BlockKind aynı değer, Calendar ile simetri için)
        public byte Kind { get; set; }
        public byte BlockKind => Kind;
        public string? Reason { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public decimal? Price { get; set; }
        public bool IsPaid { get; set; }
        public string? Note { get; set; }
        public Guid? SeriesId { get; set; }
    }

    public class BlockSaveInput
    {
        public int? SlotBlockId { get; set; }
        public int PitchId { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public int Kind { get; set; }
        public string? Reason { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public decimal? Price { get; set; }
        public bool? IsPaid { get; set; }
        public string? Note { get; set; }
        /// <summary>Yeni harici rezervasyon: kaç hafta (1 = tek sefer). Dolu haftalar atlanır.</summary>
        public int? RepeatWeeks { get; set; }
    }

    public class BlockPaidInput
    {
        public int SlotBlockId { get; set; }
        public bool IsPaid { get; set; }
    }

    // ------------------------------------------------------------------ Ek paket: harici rezervasyon fiyatı, giderler, finans

    public class OwnerQuoteInput
    {
        public int PitchId { get; set; }
        public DateTime? Start { get; set; }
        public int DurationMinutes { get; set; } = 60;
    }

    public class OwnerQuoteDto
    {
        public decimal PitchTotal { get; set; }
        public List<QuoteLineDto> Lines { get; set; } = new();
    }

    public class VenueExpenseDto
    {
        public int ExpenseId { get; set; }
        public int StadiumId { get; set; }
        // "yyyy-MM-dd"
        public string Date { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ExpenseSaveInput
    {
        public int? ExpenseId { get; set; }
        public int StadiumId { get; set; }
        public DateTime? Date { get; set; }
        public string? Category { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
    }

    public class ExpenseIdInput
    {
        public int ExpenseId { get; set; }
    }

    public class FinanceIncomeDto
    {
        public decimal Matches { get; set; }
        public decimal Manual { get; set; }
        public decimal Total { get; set; }
    }

    public class FinancePeriodDto
    {
        public decimal Income { get; set; }
        public decimal Expenses { get; set; }
        public decimal Net { get; set; }
    }

    public class FinanceCountsDto
    {
        public int Matches { get; set; }
        public int ManualBookings { get; set; }
        public double HoursSold { get; set; }
    }

    public class FinanceDailyDto
    {
        // "yyyy-MM-dd"
        public string Date { get; set; } = string.Empty;
        public decimal Income { get; set; }
        public decimal Expense { get; set; }
    }

    public class FinancePitchDto
    {
        public int PitchId { get; set; }
        public string? Name { get; set; }
        public decimal Income { get; set; }
        public double Hours { get; set; }
    }

    public class FinanceSourceDto
    {
        public string Source { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class FinanceCategoryDto
    {
        public string Category { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class FinanceMonthDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Income { get; set; }
        public decimal Expenses { get; set; }
        public decimal Net { get; set; }
    }

    public class FinanceTransactionDto
    {
        public DateTime Date { get; set; }
        // "income" | "expense"
        public string Type { get; set; } = string.Empty;
        // "match" | "manual" | "expense"
        public string Source { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? Category { get; set; }
        public string? PitchName { get; set; }
        public decimal Amount { get; set; }
        public bool IsPaid { get; set; }
        public int RefId { get; set; }
    }

    public class OwnerFinanceDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public FinanceIncomeDto Income { get; set; } = new();
        public decimal Receivable { get; set; }
        public decimal ExpectedIncome { get; set; }
        public decimal Expenses { get; set; }
        public decimal Net { get; set; }
        public double MarginPercent { get; set; }
        public FinancePeriodDto Previous { get; set; } = new();
        public FinanceCountsDto Counts { get; set; } = new();
        public List<FinanceDailyDto> Daily { get; set; } = new();
        public List<FinancePitchDto> IncomeByPitch { get; set; } = new();
        public List<FinanceSourceDto> IncomeBySource { get; set; } = new();
        public List<FinanceCategoryDto> ExpenseByCategory { get; set; } = new();
        public List<FinanceMonthDto> Monthly { get; set; } = new();
        public List<FinanceTransactionDto> Transactions { get; set; } = new();
    }

    public class BlockIdInput
    {
        public int SlotBlockId { get; set; }
        /// <summary>Haftalık seride bu ve sonraki haftaları da sil.</summary>
        public bool AllFollowing { get; set; }
    }

    public class ReviewReplyInput
    {
        public int ReviewId { get; set; }
        public string? Reply { get; set; }
    }

    // ------------------------------------------------------------------ Admin

    public class AdminOwnerInfoDto
    {
        public int PlayerId { get; set; }
        public string? Name { get; set; }
        public string? ContactName { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public class AdminOwnerVenueDto
    {
        public int StadiumId { get; set; }
        public string? Name { get; set; }
        public bool IsVisible { get; set; }
        public bool IsListed { get; set; }
    }

    public class AdminOwnerDto
    {
        public AdminOwnerInfoDto Owner { get; set; } = new();
        public DateTime? MembershipUntil { get; set; }
        public bool MembershipSuspended { get; set; }
        public bool MembershipActive { get; set; }
        public List<AdminOwnerVenueDto> Venues { get; set; } = new();
        public DateTime? CreatedAt { get; set; }
    }

    /// <summary>Admin: tek işletmenin tüm bilgileri (tesis detayları, fotoğraflar, istatistikler).</summary>
    public class AdminOwnerDetailDto : AdminOwnerDto
    {
        public List<OwnerVenueDetailDto> VenueDetails { get; set; } = new();
        public AdminOwnerStatsDto Stats { get; set; } = new();
    }

    public class AdminOwnerStatsDto
    {
        public int TotalReservations { get; set; }    // reddedilmemiş tüm rezervasyonlar
        public int PendingCount { get; set; }
        public int UpcomingApprovedCount { get; set; }
        public int RejectedCount { get; set; }
        public decimal ApprovedRevenue { get; set; }  // onaylanmış ve oynanmış maçların TotalPrice toplamı
        public DateTime? LastReservationAt { get; set; }
    }

    public class ResetOwnerPasswordResultDto
    {
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class CreateOwnerAccountInput
    {
        public string? Name { get; set; }
        public string? ContactName { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Password { get; set; }
        public DateTime? MembershipUntil { get; set; }
    }

    public class CreateOwnerVenueInput
    {
        public string? Name { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? Address { get; set; }
        public string? Location { get; set; }
        public string? Phone { get; set; }
        public string? Description { get; set; }
    }

    public class CreateOwnerInput
    {
        public CreateOwnerAccountInput? Owner { get; set; }
        public CreateOwnerVenueInput? Venue { get; set; }
    }

    public class CreateOwnerResultDto
    {
        public int OwnerId { get; set; }
        public int StadiumId { get; set; }
    }

    public class UpdateMembershipInput
    {
        public int OwnerId { get; set; }
        public DateTime? MembershipUntil { get; set; }
        public bool Suspended { get; set; }
    }

    public class AssignVenueInput
    {
        public int StadiumId { get; set; }
        public int OwnerId { get; set; }
    }

    public class SetListedInput
    {
        public int StadiumId { get; set; }
        public bool IsListed { get; set; }
    }

    public class OsmCandidateDto
    {
        public long OsmId { get; set; }
        public string? Name { get; set; }
        public double Lat { get; set; }
        public double Lon { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Website { get; set; }
        public string? OpeningHours { get; set; }
        public bool AlreadyExists { get; set; }
    }

    public class ImportCandidateInput
    {
        public long OsmId { get; set; }
        public string? Name { get; set; }
        public double Lat { get; set; }
        public double Lon { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? Phone { get; set; }
    }

    /// <summary>OsmCandidates "alreadyExists" hesabı için mevcut tesislerin kaynak bilgisi.</summary>
    public class VenueGeoRefDto
    {
        public int StadiumId { get; set; }
        public long? OsmId { get; set; }
        public string? Location { get; set; }
    }
}
