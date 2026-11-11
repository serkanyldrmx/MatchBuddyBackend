using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.Entities.DTOs;

namespace MatchBuddy.DataAccess.Abstract
{
    /// <summary>Anlaşmalı tesisler, rezervasyonlar, işletme paneli ve admin işlemleri (docs/venues-api.md).</summary>
    public interface IVenueDal
    {
        // ---- Oyuncu
        List<VenueListItemDto> List(string? city, string? query, DateTime date);
        OperationResult<VenueDetailDto> Detail(int stadiumId, int viewerId, bool isAdmin);
        OperationResult<AvailabilityDto> Availability(int stadiumId, DateTime date, int viewerId, bool isAdmin);
        OperationResult<QuoteDto> Quote(QuoteInput input);
        OperationResult<bool> Review(int me, int matchId, byte rating, string? comment);

        // ---- Maç kurma / rakip / durum
        OperationResult<int> CreateReservation(int creatorId, SaveMatchInput input);
        // Harici (anlaşmasız) saha (docs/external-venues-api.md)
        OperationResult<int> CreateExternalMatch(int creatorId, ExternalMatchInput input);
        OperationResult<bool> UpdateExternalMatch(int actorId, bool isAdmin, UpdateExternalMatchInput input);
        List<ExternalVenueDto> MyExternalVenues(int me);
        // Admin: harici sahalardan satış listesi (docs/engagement-api.md §4)
        List<ExternalVenueLeadDto> ExternalLeads(string? query);
        List<LinkTargetDto> LinkTargets(string? query);
        OperationResult<int> LinkExternal(LinkExternalInput input);
        OperationResult<bool> ClaimOpponent(int me, int matchId, int teamId);
        // ownerId null = admin (geçersiz kılma: her durumdan her duruma)
        // status: 2 onay, 3 red (admin için 1 = tekrar beklemeye de alınabilir)
        OperationResult<bool> SetMatchStatus(int matchId, byte status, string? reason, int? ownerId);

        // ---- İşletme
        OwnerVenuesDto MyVenues(int me);
        OperationResult<OwnerVenueDetailDto> OwnerVenue(int me, int stadiumId);
        OperationResult<OwnerDashboardDto> Dashboard(int me, int? stadiumId);
        OperationResult<List<ReservationDto>> Reservations(int me, int? stadiumId, byte? status, DateTime? from, DateTime? to);
        OperationResult<OwnerCalendarDto> Calendar(int me, int stadiumId, DateTime from, DateTime to);
        OperationResult<bool> UpdateVenue(int me, VenueUpdateInput input);
        OperationResult<List<VenuePhotoDto>> Photos(int me, int stadiumId);
        // Yazmadan önce yetki/üyelik kontrolü (dosya diske yazılmadan önce)
        OperationResult<bool> CheckCanAddPhoto(int me, int stadiumId);
        OperationResult<VenueSettingsDto> GetSettings(int me, int stadiumId);
        OperationResult<VenueSettingsDto> SaveSettings(int me, VenueSettingsInput input);
        OperationResult<VenuePhotoDto> AddPhoto(int me, int stadiumId, string url);
        // Data = silinen dosyanın url'i
        OperationResult<string> DeletePhoto(int me, int photoId);
        OperationResult<bool> SetCover(int me, int photoId);
        OperationResult<List<PitchDto>> Pitches(int me, int stadiumId);
        OperationResult<int> SavePitch(int me, PitchSaveInput input);
        OperationResult<bool> DeletePitch(int me, int pitchId);
        OperationResult<List<OpeningHourDto>> OpeningHours(int me, int stadiumId);
        OperationResult<bool> SaveOpeningHours(int me, int stadiumId, List<(int Day, bool IsClosed, TimeSpan Open, TimeSpan Close)> days);
        OperationResult<List<PriceRuleDto>> PriceRules(int me, int stadiumId);
        OperationResult<int> SavePriceRule(int me, PriceRuleDto input);
        OperationResult<bool> DeletePriceRule(int me, int priceRuleId);
        OperationResult<List<CampaignDto>> Campaigns(int me, int stadiumId);
        OperationResult<int> SaveCampaign(int me, CampaignSaveInput input);
        OperationResult<bool> DeleteCampaign(int me, int campaignId);
        OperationResult<List<SlotBlockDto>> Blocks(int me, int stadiumId, DateTime from, DateTime to);
        OperationResult<int> SaveBlock(int me, BlockSaveInput input);
        OperationResult<bool> DeleteBlock(int me, int slotBlockId, bool allFollowing = false);
        OperationResult<SlotDealAudienceDto> SlotDealAudience(int me, int stadiumId);
        OperationResult<SlotDealResultDto> CreateSlotDeal(int me, SlotDealInput input);
        OperationResult<List<OwnerCustomerDto>> Customers(int me, int stadiumId, string? q);
        OperationResult<bool> SetBlockPaid(int me, int slotBlockId, bool isPaid);
        OperationResult<OwnerQuoteDto> OwnerQuote(int me, int pitchId, DateTime start, int durationMinutes);
        OperationResult<List<VenueExpenseDto>> Expenses(int me, int stadiumId, DateTime? from, DateTime? to);
        OperationResult<int> SaveExpense(int me, ExpenseSaveInput input);
        OperationResult<bool> DeleteExpense(int me, int expenseId);
        OperationResult<OwnerFinanceDto> Finance(int me, int? stadiumId, int year, int month);
        OperationResult<List<VenueReviewDto>> OwnerReviews(int me, int stadiumId);
        OperationResult<bool> ReplyReview(int me, int reviewId, string reply);

        // ---- Admin
        List<AdminOwnerDto> Owners();
        OperationResult<AdminOwnerDetailDto> OwnerDetail(int ownerId);
        OperationResult<string> SetOwnerPassword(int ownerId, string passwordHash);
        bool UserNameTaken(string userName);
        OperationResult<CreateOwnerResultDto> CreateOwner(CreateOwnerInput input, string passwordHash, List<string> photoUrls);
        OperationResult<bool> UpdateMembership(int ownerId, DateTime? membershipUntil, bool suspended);
        OperationResult<bool> AssignVenue(int stadiumId, int ownerId);
        OperationResult<bool> SetListed(int stadiumId, bool isListed);
        List<VenueGeoRefDto> GeoRefs();
        OperationResult<int> ImportCandidate(ImportCandidateInput input);
    }
}
