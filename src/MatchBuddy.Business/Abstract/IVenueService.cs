using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.Entities.DTOs;

namespace MatchBuddy.Business.Abstract
{
    /// <summary>Anlaşmalı tesisler (docs/venues-api.md): girdi doğrulaması burada, iş kuralları EFVenueDal'da.</summary>
    public interface IVenueService
    {
        // Oyuncu
        List<VenueListItemDto> List(string? city, string? query, DateTime? date);
        OperationResult<VenueDetailDto> Detail(int stadiumId, int viewerId, bool isAdmin);
        OperationResult<AvailabilityDto> Availability(int stadiumId, DateTime? date, int viewerId, bool isAdmin);
        OperationResult<QuoteDto> Quote(QuoteInput input);
        OperationResult<bool> Review(int me, ReviewInput input);

        // Maç
        OperationResult<int> CreateReservation(int creatorId, SaveMatchInput input);
        OperationResult<bool> ClaimOpponent(int me, ClaimOpponentInput input);
        OperationResult<bool> RespondReservation(int ownerId, RespondReservationInput input);
        OperationResult<bool> AdminSetStatus(int matchId, byte status, string? reason);

        // İşletme
        OwnerVenuesDto MyVenues(int me);
        OperationResult<OwnerVenueDetailDto> OwnerVenue(int me, int stadiumId);
        OperationResult<OwnerDashboardDto> Dashboard(int me, int? stadiumId);
        OperationResult<List<ReservationDto>> Reservations(int me, int? stadiumId, byte? status, DateTime? from, DateTime? to);
        OperationResult<OwnerCalendarDto> Calendar(int me, int stadiumId, DateTime? from, DateTime? to);
        OperationResult<bool> UpdateVenue(int me, VenueUpdateInput input);
        OperationResult<List<VenuePhotoDto>> Photos(int me, int stadiumId);
        OperationResult<bool> CheckCanAddPhoto(int me, int stadiumId);
        OperationResult<VenueSettingsDto> GetSettings(int me, int stadiumId);
        OperationResult<VenueSettingsDto> SaveSettings(int me, VenueSettingsInput input);
        OperationResult<VenuePhotoDto> AddPhoto(int me, int stadiumId, string url);
        OperationResult<string> DeletePhoto(int me, int photoId);
        OperationResult<bool> SetCover(int me, int photoId);
        OperationResult<List<PitchDto>> Pitches(int me, int stadiumId);
        OperationResult<int> SavePitch(int me, PitchSaveInput input);
        OperationResult<bool> DeletePitch(int me, int pitchId);
        OperationResult<List<OpeningHourDto>> OpeningHours(int me, int stadiumId);
        OperationResult<bool> SaveOpeningHours(int me, OpeningHoursInput input);
        OperationResult<List<PriceRuleDto>> PriceRules(int me, int stadiumId);
        OperationResult<int> SavePriceRule(int me, PriceRuleDto input);
        OperationResult<bool> DeletePriceRule(int me, int priceRuleId);
        OperationResult<List<CampaignDto>> Campaigns(int me, int stadiumId);
        OperationResult<int> SaveCampaign(int me, CampaignSaveInput input);
        OperationResult<bool> DeleteCampaign(int me, int campaignId);
        OperationResult<List<SlotBlockDto>> Blocks(int me, int stadiumId, DateTime? from, DateTime? to);
        OperationResult<int> SaveBlock(int me, BlockSaveInput input);
        OperationResult<bool> DeleteBlock(int me, int slotBlockId, bool allFollowing = false);
        OperationResult<SlotDealAudienceDto> SlotDealAudience(int me, int stadiumId);
        OperationResult<SlotDealResultDto> CreateSlotDeal(int me, SlotDealInput input);
        OperationResult<List<OwnerCustomerDto>> Customers(int me, int stadiumId, string? q);
        OperationResult<bool> SetBlockPaid(int me, BlockPaidInput input);
        OperationResult<OwnerQuoteDto> OwnerQuote(int me, OwnerQuoteInput input);
        OperationResult<List<VenueExpenseDto>> Expenses(int me, int stadiumId, DateTime? from, DateTime? to);
        OperationResult<int> SaveExpense(int me, ExpenseSaveInput input);
        OperationResult<bool> DeleteExpense(int me, int expenseId);
        OperationResult<OwnerFinanceDto> Finance(int me, int? stadiumId, int? year, int? month);
        OperationResult<List<VenueReviewDto>> OwnerReviews(int me, int stadiumId);
        OperationResult<bool> ReplyReview(int me, ReviewReplyInput input);

        // Admin
        List<AdminOwnerDto> Owners();
        OperationResult<AdminOwnerDetailDto> OwnerDetail(int ownerId);
        OperationResult<ResetOwnerPasswordResultDto> ResetOwnerPassword(int ownerId);
        // Alan bazlı doğrulama: (field, message) ya da null
        (string Field, AppError Error)? ValidateCreateOwner(CreateOwnerInput? input, int photoCount);
        OperationResult<CreateOwnerResultDto> CreateOwner(CreateOwnerInput input, List<string> photoUrls);
        OperationResult<bool> UpdateMembership(UpdateMembershipInput input);
        OperationResult<bool> AssignVenue(AssignVenueInput input);
        OperationResult<bool> SetListed(SetListedInput input);
        List<VenueGeoRefDto> GeoRefs();
        OperationResult<int> ImportCandidate(ImportCandidateInput input);
    }
}
