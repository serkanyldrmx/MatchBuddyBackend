using MatcBuddy.Business.Abstract;
using MatcBuddy.Business.Constants;
using MatcBuddy.Business.Security;
using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.DataAccess.Concrete;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Business.Concrete
{
    /// <summary>Girdi doğrulaması; iş kuralları (sahiplik, üyelik, müsaitlik, fiyat) EFVenueDal'da.</summary>
    public class VenueManager : IVenueService
    {
        private static decimal MaxPrice => P.Dec(ParameterKeys.Venue.MaxPrice);
        private readonly IVenueDal _venueDal;

        public VenueManager(IVenueDal venueDal)
        {
            _venueDal = venueDal;
        }

        private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private static string L(string labelKey) => Msg.Get(labelKey);

        private static OperationResult<T> Bad<T>(string code, params object?[] args) => OperationResult<T>.BadRequest(code, args);

        private static OperationResult<T> Bad<T>(AppError error) => OperationResult<T>.BadRequest(error);

        private static AppError TooLong(string labelKey, int max) => AppError.Bad(ErrorCodes.Validation.TooLong, L(labelKey), max);

        private static AppError TooShort(string labelKey, int min) => AppError.Bad(ErrorCodes.Validation.TooShort, L(labelKey), min);

        private static AppError? CheckLength(string? value, int max, string labelKey) =>
            value != null && value.Length > max ? AppError.Bad(ErrorCodes.Validation.TooLong, L(labelKey), max) : null;

        private static AppError? CheckPrice(decimal? price, string labelKey, bool required = false)
        {
            if (price == null) return required ? AppError.Bad(ErrorCodes.Venue.FieldMustBeEntered, L(labelKey)) : null;
            if (price < 0 || price > MaxPrice) return AppError.Bad(ErrorCodes.Validation.OutOfRange, L(labelKey), 0, MaxPrice);
            return null;
        }

        private static AppError? CheckMaskAndHours(int daysMask, int fromHour, int toHour)
        {
            if (daysMask < 1 || daysMask > 127) return AppError.Bad(ErrorCodes.Venue.InvalidDaysMask);
            if (fromHour < 0 || fromHour > 23 || toHour < 1 || toHour > 24 || fromHour >= toHour)
                return AppError.Bad(ErrorCodes.Venue.InvalidHourRange);
            return null;
        }

        // ------------------------------------------------------------------ Oyuncu

        public List<VenueListItemDto> List(string? city, string? query, DateTime? date) =>
            _venueDal.List(city, query, (date ?? DateTime.Today).Date);

        public OperationResult<VenueDetailDto> Detail(int stadiumId, int viewerId, bool isAdmin) =>
            _venueDal.Detail(stadiumId, viewerId, isAdmin);

        public OperationResult<AvailabilityDto> Availability(int stadiumId, DateTime? date, int viewerId, bool isAdmin) =>
            _venueDal.Availability(stadiumId, (date ?? DateTime.Today).Date, viewerId, isAdmin);

        public OperationResult<QuoteDto> Quote(QuoteInput input)
        {
            if (input == null || input.PitchId <= 0) return Bad<QuoteDto>(ErrorCodes.Venue.PitchRequired);
            return _venueDal.Quote(input);
        }

        public OperationResult<bool> Review(int me, ReviewInput input)
        {
            if (input == null) return Bad<bool>(ErrorCodes.Venue.ReviewEmpty);
            if (input.Rating < 1 || input.Rating > 5) return Bad<bool>(ErrorCodes.Validation.OutOfRange, L(MessageKeys.Venue.LabelRating), 1, 5);
            var comment = Clean(input.Comment);
            var err = CheckLength(comment, 500, MessageKeys.Venue.LabelComment);
            if (err != null) return Bad<bool>(err);
            return _venueDal.Review(me, input.MatchId, (byte)input.Rating, comment);
        }

        // ------------------------------------------------------------------ Maç

        public OperationResult<int> CreateReservation(int creatorId, SaveMatchInput input)
        {
            if (input == null || input.PitchId is not > 0 || input.MatchDate == null)
                return Bad<int>(ErrorCodes.Venue.ChooseSlot);
            input.MatchName = Clean(input.MatchName);
            if (input.MatchName == null || input.MatchName.Length < 5) return Bad<int>(Messages.MatchNameInvalid);
            var err = CheckLength(input.MatchName, 100, MessageKeys.Venue.LabelMatchName) ?? CheckLength(input.Description, 500, MessageKeys.Venue.LabelDescription);
            if (err != null) return Bad<int>(err);
            if (input.DurationMinutes.HasValue && !VenuePricing.AllowedDurations.Contains(input.DurationMinutes.Value))
                return Bad<int>(ErrorCodes.Venue.InvalidDuration);
            if (input.CleatCount < 0) return Bad<int>(ErrorCodes.Validation.NegativeNotAllowed, L(MessageKeys.Venue.LabelCleatCount));
            return _venueDal.CreateReservation(creatorId, input);
        }

        // ------------------------------------------------------------------ Harici saha (docs/external-venues-api.md)

        private const int ExternalVenueNameMin = 2;
        private const int ExternalVenueNameMax = 100;
        private const decimal ExternalMaxPrice = 100000m;

        /// <summary>Harici saha alanlarını temizler ve doğrular (ad, adres, şehir, ilçe, telefon, konum).</summary>
        private static AppError? CheckExternalVenue(ref string? name, ref string? address, ref string? city, ref string? district,
            ref string? phone, ref string? location, bool nameRequired)
        {
            name = Clean(name);
            address = Clean(address);
            city = Clean(city);
            district = Clean(district);
            phone = Clean(phone);
            location = Clean(location);
            if (name == null ? nameRequired : name.Length < ExternalVenueNameMin || name.Length > ExternalVenueNameMax)
                return AppError.Bad(ErrorCodes.External.VenueNameRequired, ExternalVenueNameMin, ExternalVenueNameMax);
            if (location != null)
            {
                if (!VenueGeo.TryParseLocation(location, out var lat, out var lon)) return AppError.Bad(ErrorCodes.Venue.InvalidLocationFormat);
                location = VenueGeo.Format(lat, lon);
            }
            return CheckLength(address, 300, MessageKeys.Venue.LabelAddress)
                   ?? CheckLength(city, 60, MessageKeys.Venue.LabelCity)
                   ?? CheckLength(district, 60, MessageKeys.Venue.LabelDistrict)
                   ?? CheckLength(phone, 30, MessageKeys.Venue.LabelPhone);
        }

        private static AppError? CheckExternalWhen(DateTime? date, int? duration, decimal? price)
        {
            if (date == null) return AppError.Bad(ErrorCodes.External.DateRequired);
            if (date.Value < DateTime.Now.AddMinutes(-5)) return AppError.Bad(ErrorCodes.External.PastDate);
            var maxDays = P.Int(ParameterKeys.Booking.DefaultMaxAdvanceDays);
            if (date.Value > DateTime.Now.AddDays(maxDays)) return AppError.Bad(ErrorCodes.External.TooFarAhead, maxDays);
            if (duration.HasValue && !VenuePricing.AllowedDurations.Contains(duration.Value)) return AppError.Bad(ErrorCodes.Venue.InvalidDuration);
            if (price is < 0 or > ExternalMaxPrice) return AppError.Bad(ErrorCodes.External.InvalidPrice, ExternalMaxPrice);
            return null;
        }

        public OperationResult<int> CreateExternalMatch(int creatorId, ExternalMatchInput input)
        {
            if (input == null) return Bad<int>(ErrorCodes.External.DateRequired);
            input.MatchName = Clean(input.MatchName);
            if (input.MatchName == null || input.MatchName.Length < 5) return Bad<int>(Messages.MatchNameInvalid);
            input.Description = Clean(input.Description);
            string? name = input.VenueName, address = input.Address, city = input.City, district = input.District, phone = input.Phone, location = input.Location;
            var err = CheckLength(input.MatchName, 100, MessageKeys.Venue.LabelMatchName)
                      ?? CheckLength(input.Description, 500, MessageKeys.Venue.LabelDescription)
                      ?? CheckExternalVenue(ref name, ref address, ref city, ref district, ref phone, ref location, nameRequired: input.ExternalVenueId is not > 0)
                      ?? CheckExternalWhen(input.MatchDate, input.DurationMinutes, input.TotalPrice);
            if (err != null) return Bad<int>(err);
            (input.VenueName, input.Address, input.City, input.District, input.Phone, input.Location) = (name, address, city, district, phone, location);
            return _venueDal.CreateExternalMatch(creatorId, input);
        }

        public OperationResult<bool> UpdateExternalMatch(int actorId, bool isAdmin, UpdateExternalMatchInput input)
        {
            if (input == null || input.MatchId <= 0) return Bad<bool>(ErrorCodes.Match.NotFound);
            string? name = input.VenueName, address = input.Address, city = null, district = null, phone = input.Phone, location = input.Location;
            var err = CheckExternalVenue(ref name, ref address, ref city, ref district, ref phone, ref location, nameRequired: false)
                      ?? (input.MatchDate.HasValue || input.DurationMinutes.HasValue || input.TotalPrice.HasValue
                          ? CheckExternalWhen(input.MatchDate ?? DateTime.Now.AddHours(1), input.DurationMinutes, input.TotalPrice)
                          : null);
            if (err != null) return Bad<bool>(err);
            (input.VenueName, input.Address, input.Phone, input.Location) = (name, address, phone, location);
            return _venueDal.UpdateExternalMatch(actorId, isAdmin, input);
        }

        public List<ExternalVenueDto> MyExternalVenues(int me) => _venueDal.MyExternalVenues(me);

        public List<ExternalVenueLeadDto> ExternalLeads(string? query) => _venueDal.ExternalLeads(query);

        public List<LinkTargetDto> LinkTargets(string? query) => _venueDal.LinkTargets(query);

        public OperationResult<int> LinkExternal(LinkExternalInput? input)
        {
            if (input == null || input.StadiumIds == null || input.StadiumIds.Count == 0) return Bad<int>(ErrorCodes.External.LinkNoVenues);
            if (!input.CreateCandidate && input.TargetStadiumId is not > 0) return Bad<int>(ErrorCodes.External.LinkTargetInvalid);
            if (input.CreateCandidate)
            {
                string? name = input.Name, address = input.Address, city = input.City, district = input.District, phone = input.Phone, location = input.Location;
                var err = CheckExternalVenue(ref name, ref address, ref city, ref district, ref phone, ref location, nameRequired: false);
                if (err != null) return Bad<int>(err);
                (input.Name, input.Address, input.City, input.District, input.Phone, input.Location) = (name, address, city, district, phone, location);
            }
            return _venueDal.LinkExternal(input);
        }

        public OperationResult<bool> ClaimOpponent(int me, ClaimOpponentInput input)
        {
            if (input == null || input.MatchId <= 0 || input.TeamId <= 0) return Bad<bool>(ErrorCodes.Venue.MatchAndTeamRequired);
            return _venueDal.ClaimOpponent(me, input.MatchId, input.TeamId);
        }

        public OperationResult<bool> RespondReservation(int ownerId, RespondReservationInput input)
        {
            if (input == null || input.MatchId <= 0) return Bad<bool>(ErrorCodes.Venue.ReservationRequired);
            return _venueDal.SetMatchStatus(input.MatchId, input.Approve ? MatchStatus.Approved : MatchStatus.Rejected, input.Reason, ownerId);
        }

        public OperationResult<bool> AdminSetStatus(int matchId, byte status, string? reason) =>
            _venueDal.SetMatchStatus(matchId, status, reason, null);

        // ------------------------------------------------------------------ İşletme

        public OwnerVenuesDto MyVenues(int me) => _venueDal.MyVenues(me);
        public OperationResult<OwnerVenueDetailDto> OwnerVenue(int me, int stadiumId) => _venueDal.OwnerVenue(me, stadiumId);
        public OperationResult<OwnerDashboardDto> Dashboard(int me, int? stadiumId) => _venueDal.Dashboard(me, stadiumId);

        public OperationResult<List<ReservationDto>> Reservations(int me, int? stadiumId, byte? status, DateTime? from, DateTime? to) =>
            _venueDal.Reservations(me, stadiumId, status, from, to);

        public OperationResult<OwnerCalendarDto> Calendar(int me, int stadiumId, DateTime? from, DateTime? to)
        {
            var today = DateTime.Today;
            var start = from ?? today.AddDays(-VenuePricing.DayIndex(today));
            var end = to ?? start.AddDays(7);
            if (end <= start) return Bad<OwnerCalendarDto>(ErrorCodes.Common.InvalidDateRange);
            var maxDays = P.Int(ParameterKeys.Venue.CalendarMaxDays);
            if ((end - start).TotalDays > maxDays) return Bad<OwnerCalendarDto>(ErrorCodes.Venue.CalendarRangeTooLong, maxDays);
            return _venueDal.Calendar(me, stadiumId, start, end);
        }

        public OperationResult<bool> UpdateVenue(int me, VenueUpdateInput input)
        {
            if (input == null) return Bad<bool>(ErrorCodes.Venue.VenueInfoEmpty);
            input.Name = Clean(input.Name);
            input.Address = Clean(input.Address);
            input.Location = Clean(input.Location);
            input.Description = Clean(input.Description);
            input.Phone = Clean(input.Phone);
            input.City = Clean(input.City);
            input.District = Clean(input.District);
            input.CancellationPolicy = Clean(input.CancellationPolicy);
            if (input.Name == null || input.Name.Length < 3) return Bad<bool>(ErrorCodes.Validation.TooShort, L(MessageKeys.Venue.LabelVenueName), 3);
            if (input.Address == null) return Bad<bool>(ErrorCodes.Venue.AddressEmpty);
            if (!VenueGeo.TryParseLocation(input.Location, out var lat, out var lon))
                return Bad<bool>(ErrorCodes.Venue.InvalidLocationFormat);
            input.Location = VenueGeo.Format(lat, lon);
            var err = CheckLength(input.Name, 250, MessageKeys.Venue.LabelVenueName) ?? CheckLength(input.Address, 300, MessageKeys.Venue.LabelAddress)
                      ?? CheckLength(input.Description, 500, MessageKeys.Venue.LabelDescription) ?? CheckLength(input.Phone, 30, MessageKeys.Venue.LabelPhone)
                      ?? CheckLength(input.City, 60, MessageKeys.Venue.LabelCity) ?? CheckLength(input.District, 60, MessageKeys.Venue.LabelDistrict)
                      ?? CheckLength(input.CancellationPolicy, 500, MessageKeys.Venue.LabelCancellationPolicy)
                      ?? CheckPrice(input.CleatRentalPrice, MessageKeys.Venue.LabelCleatRentalPrice, input.CleatRental)
                      ?? CheckPrice(input.CameraPrice, MessageKeys.Venue.LabelCameraPrice, input.HasCamera);
            if (err != null) return Bad<bool>(err);
            return _venueDal.UpdateVenue(me, input);
        }

        public OperationResult<List<VenuePhotoDto>> Photos(int me, int stadiumId) => _venueDal.Photos(me, stadiumId);
        public OperationResult<bool> CheckCanAddPhoto(int me, int stadiumId) => _venueDal.CheckCanAddPhoto(me, stadiumId);

        public OperationResult<VenueSettingsDto> GetSettings(int me, int stadiumId) => _venueDal.GetSettings(me, stadiumId);

        public OperationResult<VenueSettingsDto> SaveSettings(int me, VenueSettingsInput input)
        {
            if (input == null || input.StadiumId <= 0) return Bad<VenueSettingsDto>(ErrorCodes.Venue.NotFound);
            AppError? Range(int? value, string key, string labelKey)
            {
                var d = ParameterCatalog.Find(key)!;
                int min = int.Parse(d.MinValue!), max = int.Parse(d.MaxValue!);
                return value is { } v && (v < min || v > max)
                    ? AppError.Bad(ErrorCodes.VenueSettings.OutOfRange, L(labelKey), min, max) : null;
            }
            var err = Range(input.MinBookingNoticeHours, ParameterKeys.Booking.DefaultMinNoticeHours, MessageKeys.Venue.LabelSettingsMinNotice)
                      ?? Range(input.FreeCancellationHours, ParameterKeys.Booking.DefaultFreeCancellationHours, MessageKeys.Venue.LabelSettingsFreeCancel)
                      ?? Range(input.MaxAdvanceBookingDays, ParameterKeys.Booking.DefaultMaxAdvanceDays, MessageKeys.Venue.LabelSettingsMaxAdvance);
            if (err != null) return OperationResult<VenueSettingsDto>.Fail(err);
            return _venueDal.SaveSettings(me, input);
        }
        public OperationResult<VenuePhotoDto> AddPhoto(int me, int stadiumId, string url) => _venueDal.AddPhoto(me, stadiumId, url);
        public OperationResult<string> DeletePhoto(int me, int photoId) => _venueDal.DeletePhoto(me, photoId);
        public OperationResult<bool> SetCover(int me, int photoId) => _venueDal.SetCover(me, photoId);

        public OperationResult<List<PitchDto>> Pitches(int me, int stadiumId) => _venueDal.Pitches(me, stadiumId);

        public OperationResult<int> SavePitch(int me, PitchSaveInput input)
        {
            if (input == null) return Bad<int>(ErrorCodes.Venue.PitchInfoEmpty);
            input.Name = Clean(input.Name);
            if (input.Name == null) return Bad<int>(ErrorCodes.Venue.PitchNameEmpty);
            var err = CheckLength(input.Name, 60, MessageKeys.Venue.LabelPitchName);
            if (err != null) return Bad<int>(err);
            if (input.Surface < 1 || input.Surface > 4) return Bad<int>(ErrorCodes.Venue.InvalidSurface);
            int minSide = P.Int(ParameterKeys.Venue.PlayersPerSideMin), maxSide = P.Int(ParameterKeys.Venue.PlayersPerSideMax);
            if (input.PlayersPerSide < minSide || input.PlayersPerSide > maxSide) return Bad<int>(ErrorCodes.Validation.OutOfRange, L(MessageKeys.Venue.LabelPlayersPerSide), minSide, maxSide);
            if (input.BasePrice <= 0 || input.BasePrice > MaxPrice) return Bad<int>(ErrorCodes.Venue.PriceOutOfRange, L(MessageKeys.Venue.LabelHourlyPrice), MaxPrice);
            return _venueDal.SavePitch(me, input);
        }

        public OperationResult<bool> DeletePitch(int me, int pitchId) => _venueDal.DeletePitch(me, pitchId);

        public OperationResult<List<OpeningHourDto>> OpeningHours(int me, int stadiumId) => _venueDal.OpeningHours(me, stadiumId);

        public OperationResult<bool> SaveOpeningHours(int me, OpeningHoursInput input)
        {
            if (input?.Days == null || input.Days.Count != 7) return Bad<bool>(ErrorCodes.Venue.OpeningHoursSevenDays);
            if (input.Days.Select(d => d.DayOfWeek).Distinct().Count() != 7 || input.Days.Any(d => d.DayOfWeek < 0 || d.DayOfWeek > 6))
                return Bad<bool>(ErrorCodes.Venue.OpeningHoursInvalidDays);
            var days = new List<(int, bool, TimeSpan, TimeSpan)>();
            foreach (var d in input.Days.OrderBy(d => d.DayOfWeek))
            {
                TimeSpan open = VenuePricing.DefaultOpen, close = VenuePricing.DefaultClose;
                var hasOpen = VenuePricing.TryParseTime(d.OpenTime, out var o);
                var hasClose = VenuePricing.TryParseTime(d.CloseTime, out var c);
                if (!d.IsClosed && (!hasOpen || !hasClose))
                    return Bad<bool>(ErrorCodes.Venue.OpeningHoursInvalidTime);
                if (hasOpen) open = o;
                if (hasClose) close = c;
                days.Add((d.DayOfWeek, d.IsClosed, open, close));
            }
            return _venueDal.SaveOpeningHours(me, input.StadiumId, days);
        }

        public OperationResult<List<PriceRuleDto>> PriceRules(int me, int stadiumId) => _venueDal.PriceRules(me, stadiumId);

        public OperationResult<int> SavePriceRule(int me, PriceRuleDto input)
        {
            if (input == null) return Bad<int>(ErrorCodes.Venue.PriceRuleEmpty);
            var err = CheckMaskAndHours(input.DaysMask, input.FromHour, input.ToHour);
            if (err != null) return Bad<int>(err);
            if (input.PricePerHour <= 0 || input.PricePerHour > MaxPrice) return Bad<int>(ErrorCodes.Venue.PriceOutOfRange, L(MessageKeys.Venue.LabelHourlyPrice), MaxPrice);
            return _venueDal.SavePriceRule(me, input);
        }

        public OperationResult<bool> DeletePriceRule(int me, int priceRuleId) => _venueDal.DeletePriceRule(me, priceRuleId);

        public OperationResult<List<CampaignDto>> Campaigns(int me, int stadiumId) => _venueDal.Campaigns(me, stadiumId);

        public OperationResult<int> SaveCampaign(int me, CampaignSaveInput input)
        {
            if (input == null) return Bad<int>(ErrorCodes.Venue.CampaignEmpty);
            input.Title = Clean(input.Title);
            input.Description = Clean(input.Description);
            if (input.Title == null) return Bad<int>(ErrorCodes.Venue.CampaignTitleEmpty);
            var err = CheckLength(input.Title, 80, MessageKeys.Venue.LabelTitle) ?? CheckLength(input.Description, 300, MessageKeys.Venue.LabelDescription)
                      ?? CheckMaskAndHours(input.DaysMask, input.FromHour, input.ToHour);
            if (err != null) return Bad<int>(err);
            if (input.StartDate == null || input.EndDate == null) return Bad<int>(ErrorCodes.Venue.StartEndDateRequired);
            if (input.EndDate.Value.Date < input.StartDate.Value.Date) return Bad<int>(ErrorCodes.Venue.EndDateBeforeStart);
            if (input.DiscountPercent.HasValue == input.FixedPrice.HasValue)
                return Bad<int>(ErrorCodes.Venue.DiscountOrFixedPrice);
            var maxDiscount = P.Int(ParameterKeys.Venue.MaxDiscountPercent);
            if (input.DiscountPercent < 1 || input.DiscountPercent > maxDiscount) return Bad<int>(ErrorCodes.Validation.OutOfRange, L(MessageKeys.Venue.LabelDiscountPercent), 1, maxDiscount);
            if (input.FixedPrice.HasValue && (input.FixedPrice <= 0 || input.FixedPrice > MaxPrice))
                return Bad<int>(ErrorCodes.Venue.PriceOutOfRange, L(MessageKeys.Venue.LabelFixedPrice), MaxPrice);
            return _venueDal.SaveCampaign(me, input);
        }

        public OperationResult<bool> DeleteCampaign(int me, int campaignId) => _venueDal.DeleteCampaign(me, campaignId);

        public OperationResult<List<SlotBlockDto>> Blocks(int me, int stadiumId, DateTime? from, DateTime? to)
        {
            var start = from ?? DateTime.Today;
            var end = to ?? start.AddDays(P.Int(ParameterKeys.Venue.BlockMaxDays));
            if (end <= start) return Bad<List<SlotBlockDto>>(ErrorCodes.Common.InvalidDateRange);
            return _venueDal.Blocks(me, stadiumId, start, end);
        }

        public OperationResult<int> SaveBlock(int me, BlockSaveInput input)
        {
            if (input == null || input.PitchId <= 0) return Bad<int>(ErrorCodes.Venue.PitchMustBeSelected);
            if (input.Kind != SlotBlockKind.Closed && input.Kind != SlotBlockKind.Manual)
                return Bad<int>(ErrorCodes.Venue.InvalidBlockKind);
            if (input.StartAt == null || input.EndAt == null) return Bad<int>(ErrorCodes.Venue.StartEndTimeRequired);
            if (input.EndAt <= input.StartAt) return Bad<int>(ErrorCodes.Venue.EndTimeBeforeStart);
            var maxBlockDays = P.Int(ParameterKeys.Venue.BlockMaxDays);
            if ((input.EndAt.Value - input.StartAt.Value).TotalDays > maxBlockDays) return Bad<int>(ErrorCodes.Venue.BlockTooLong, maxBlockDays);
            input.Reason = Clean(input.Reason);
            input.CustomerName = Clean(input.CustomerName);
            input.CustomerPhone = Clean(input.CustomerPhone);
            input.Note = Clean(input.Note);
            if (input.RepeatWeeks is > 1)
            {
                if (input.SlotBlockId is > 0 || input.Kind != SlotBlockKind.Manual) return Bad<int>(ErrorCodes.Owner.RepeatOnlyNewManual);
                var maxWeeks = P.Int(ParameterKeys.Venue.RepeatMaxWeeks);
                if (input.RepeatWeeks > maxWeeks) return Bad<int>(ErrorCodes.Validation.OutOfRange, L(MessageKeys.Growth.LabelRepeatWeeks), 2, maxWeeks);
            }
            if (input.Kind == SlotBlockKind.Manual)
            {
                if (input.CustomerName == null) return Bad<int>(ErrorCodes.Venue.FieldMustBeEntered, L(MessageKeys.Venue.LabelCustomerName));
                if (input.Price == null) return Bad<int>(ErrorCodes.Venue.FieldMustBeEntered, L(MessageKeys.Venue.LabelFee));
                if (input.Price < 0 || input.Price > MaxPrice) return Bad<int>(ErrorCodes.Validation.OutOfRange, L(MessageKeys.Venue.LabelFee), 0, MaxPrice);
            }
            else
            {
                // Kapalı blokta sebep zorunlu; boşsa eski davranıştaki gibi "Kapalı" yazılır
                input.Reason ??= Msg.GetDefault(MessageKeys.Venue.LabelBlockClosed);
            }
            var err = CheckLength(input.Reason, 100, MessageKeys.Venue.LabelReason) ?? CheckLength(input.CustomerName, 80, MessageKeys.Venue.LabelCustomerName)
                      ?? CheckLength(input.CustomerPhone, 30, MessageKeys.Venue.LabelPhone) ?? CheckLength(input.Note, 200, MessageKeys.Venue.LabelNote);
            if (err != null) return Bad<int>(err);
            return _venueDal.SaveBlock(me, input);
        }

        public OperationResult<bool> DeleteBlock(int me, int slotBlockId, bool allFollowing = false) =>
            _venueDal.DeleteBlock(me, slotBlockId, allFollowing);

        public OperationResult<SlotDealAudienceDto> SlotDealAudience(int me, int stadiumId) => _venueDal.SlotDealAudience(me, stadiumId);

        public OperationResult<SlotDealResultDto> CreateSlotDeal(int me, SlotDealInput input)
        {
            if (input == null || input.PitchId <= 0) return Bad<SlotDealResultDto>(ErrorCodes.Venue.PitchMustBeSelected);
            if (input.Start == null) return Bad<SlotDealResultDto>(ErrorCodes.Venue.StartTimeRequired);
            if (input.DurationMinutes < 30 || input.DurationMinutes > 240)
                return Bad<SlotDealResultDto>(ErrorCodes.Venue.DurationOutOfRange, 30, 240);
            var maxDiscount = P.Int(ParameterKeys.Venue.MaxDiscountPercent);
            if (input.DiscountPercent < 5 || input.DiscountPercent > maxDiscount)
                return Bad<SlotDealResultDto>(ErrorCodes.Validation.OutOfRange, L(MessageKeys.Growth.DealTitle), 5, maxDiscount);
            return _venueDal.CreateSlotDeal(me, input);
        }

        public OperationResult<List<OwnerCustomerDto>> Customers(int me, int stadiumId, string? q) =>
            _venueDal.Customers(me, stadiumId, q?.Length > 40 ? q[..40] : q);

        public OperationResult<bool> SetBlockPaid(int me, BlockPaidInput input)
        {
            if (input == null || input.SlotBlockId <= 0) return Bad<bool>(ErrorCodes.Venue.ReservationRequired);
            return _venueDal.SetBlockPaid(me, input.SlotBlockId, input.IsPaid);
        }

        public OperationResult<OwnerQuoteDto> OwnerQuote(int me, OwnerQuoteInput input)
        {
            if (input == null || input.PitchId <= 0) return Bad<OwnerQuoteDto>(ErrorCodes.Venue.PitchRequired);
            if (input.Start == null) return Bad<OwnerQuoteDto>(ErrorCodes.Venue.StartTimeRequired);
            if (input.DurationMinutes < 15 || input.DurationMinutes > 24 * 60)
                return Bad<OwnerQuoteDto>(ErrorCodes.Venue.DurationOutOfRange, 15, 1440);
            return _venueDal.OwnerQuote(me, input.PitchId, input.Start.Value, input.DurationMinutes);
        }

        // ------------------------------------------------------------------ Giderler / finans

        private const decimal MaxExpense = 100000000m;

        public OperationResult<List<VenueExpenseDto>> Expenses(int me, int stadiumId, DateTime? from, DateTime? to)
        {
            if (from.HasValue && to.HasValue && to.Value.Date < from.Value.Date)
                return Bad<List<VenueExpenseDto>>(ErrorCodes.Venue.EndDateBeforeStart);
            return _venueDal.Expenses(me, stadiumId, from, to);
        }

        public OperationResult<int> SaveExpense(int me, ExpenseSaveInput input)
        {
            if (input == null) return Bad<int>(ErrorCodes.Venue.ExpenseInfoEmpty);
            if (input.StadiumId <= 0 && input.ExpenseId is not > 0) return Bad<int>(ErrorCodes.Venue.VenueMustBeSelected);
            if (input.Date == null) return Bad<int>(ErrorCodes.Venue.FieldMustBeEntered, L(MessageKeys.Venue.LabelDate));
            if (input.Date.Value.Year < 2000 || input.Date.Value.Date > DateTime.Today.AddYears(1))
                return Bad<int>(ErrorCodes.Venue.InvalidDate);
            var category = Clean(input.Category);
            var canonical = ExpenseCategories.All.FirstOrDefault(c => string.Equals(c, category, StringComparison.OrdinalIgnoreCase));
            if (canonical == null) return Bad<int>(ErrorCodes.Venue.InvalidExpenseCategory, string.Join(", ", ExpenseCategories.All));
            input.Category = canonical;
            if (input.Amount <= 0) return Bad<int>(ErrorCodes.Venue.AmountMustBePositive);
            if (input.Amount > MaxExpense) return Bad<int>(ErrorCodes.Venue.AmountTooLarge, MaxExpense);
            if (decimal.Round(input.Amount, 2) != input.Amount) return Bad<int>(ErrorCodes.Venue.AmountTooManyDecimals);
            input.Description = Clean(input.Description);
            var err = CheckLength(input.Description, 200, MessageKeys.Venue.LabelDescription);
            if (err != null) return Bad<int>(err);
            return _venueDal.SaveExpense(me, input);
        }

        public OperationResult<bool> DeleteExpense(int me, int expenseId)
        {
            if (expenseId <= 0) return Bad<bool>(ErrorCodes.Venue.ExpenseMustBeSelected);
            return _venueDal.DeleteExpense(me, expenseId);
        }

        public OperationResult<OwnerFinanceDto> Finance(int me, int? stadiumId, int? year, int? month)
        {
            var today = DateTime.Today;
            var y = year ?? today.Year;
            var m = month ?? today.Month;
            if (y < 2000 || y > 2100) return Bad<OwnerFinanceDto>(ErrorCodes.Venue.InvalidYear);
            if (m < 1 || m > 12) return Bad<OwnerFinanceDto>(ErrorCodes.Validation.OutOfRange, L(MessageKeys.Venue.LabelMonth), 1, 12);
            return _venueDal.Finance(me, stadiumId, y, m);
        }

        public OperationResult<List<VenueReviewDto>> OwnerReviews(int me, int stadiumId) => _venueDal.OwnerReviews(me, stadiumId);

        public OperationResult<bool> ReplyReview(int me, ReviewReplyInput input)
        {
            var reply = Clean(input?.Reply);
            if (reply == null) return Bad<bool>(ErrorCodes.Venue.ReplyEmpty);
            var err = CheckLength(reply, 500, MessageKeys.Venue.LabelReply);
            if (err != null) return Bad<bool>(err);
            return _venueDal.ReplyReview(me, input!.ReviewId, reply);
        }

        // ------------------------------------------------------------------ Admin

        public List<AdminOwnerDto> Owners() => _venueDal.Owners();

        public OperationResult<AdminOwnerDetailDto> OwnerDetail(int ownerId) => _venueDal.OwnerDetail(ownerId);

        /// <summary>
        /// Şifreler BCrypt ile saklandığı için mevcut şifre okunamaz; admin yeni geçici şifre üretir.
        /// Düz metin şifre sadece bu yanıtta bir kez döner, hiçbir yerde saklanmaz.
        /// </summary>
        public OperationResult<ResetOwnerPasswordResultDto> ResetOwnerPassword(int ownerId)
        {
            var password = GenerateTemporaryPassword();
            var res = _venueDal.SetOwnerPassword(ownerId, PasswordHasher.Hash(password));
            if (!res.Success) return OperationResult<ResetOwnerPasswordResultDto>.NotFound(res.Code ?? ErrorCodes.Venue.OwnerNotFound, res.Args);
            return OperationResult<ResetOwnerPasswordResultDto>.Ok(new ResetOwnerPasswordResultDto { UserName = res.Data!, Password = password });
        }

        private static string GenerateTemporaryPassword()
        {
            // Karıştırılması kolay karakterler (0/O, 1/l/I) kullanılmaz
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghijkmnpqrstuvwxyz";
            const string digits = "23456789";
            const string symbols = "!@#$%*?";
            var all = upper + lower + digits + symbols;
            int R(int n) => System.Security.Cryptography.RandomNumberGenerator.GetInt32(n);
            var chars = new List<char> { upper[R(upper.Length)], lower[R(lower.Length)], digits[R(digits.Length)], symbols[R(symbols.Length)] };
            while (chars.Count < P.Int(ParameterKeys.Security.TempPasswordLength)) chars.Add(all[R(all.Length)]);
            return new string(chars.OrderBy(_ => R(10000)).ToArray());
        }

        public (string Field, AppError Error)? ValidateCreateOwner(CreateOwnerInput? input, int photoCount)
        {
            if (input?.Owner == null) return ("owner", AppError.Bad(ErrorCodes.Venue.OwnerInfoRequired));
            if (input.Venue == null) return ("venue", AppError.Bad(ErrorCodes.Venue.VenueInfoRequired));
            var o = input.Owner;
            var v = input.Venue;
            o.Name = Clean(o.Name);
            o.ContactName = Clean(o.ContactName);
            o.UserName = Clean(o.UserName);
            o.Email = Clean(o.Email);
            o.PhoneNumber = Clean(o.PhoneNumber);
            v.Name = Clean(v.Name);
            v.City = Clean(v.City);
            v.District = Clean(v.District);
            v.Address = Clean(v.Address);
            v.Location = Clean(v.Location);
            v.Phone = Clean(v.Phone);
            v.Description = Clean(v.Description);

            if (o.Name == null || o.Name.Length < 3) return ("owner.name", TooShort(MessageKeys.Venue.LabelOwnerName, 3));
            if (o.Name.Length > 100) return ("owner.name", TooLong(MessageKeys.Venue.LabelOwnerName, 100));
            if (o.ContactName != null && o.ContactName.Length > 100) return ("owner.contactName", TooLong(MessageKeys.Venue.LabelContactName, 100));
            if (o.UserName == null) return ("owner.userName", AppError.Bad(ErrorCodes.Venue.FieldRequired, L(MessageKeys.Venue.LabelUserName)));
            if (o.UserName.Length > 25) return ("owner.userName", TooLong(MessageKeys.Venue.LabelUserName, 25));
            if (_venueDal.UserNameTaken(o.UserName)) return ("owner.userName", AppError.Bad(ErrorCodes.Venue.UserNameTaken));
            var minPassword = P.Int(ParameterKeys.Security.PasswordMinLength);
            if (string.IsNullOrEmpty(o.Password) || o.Password.Length < minPassword) return ("owner.password", TooShort(MessageKeys.Venue.LabelPassword, minPassword));
            if (o.Email != null && o.Email.Length > 100) return ("owner.email", TooLong(MessageKeys.Venue.LabelEmail, 100));
            if (o.PhoneNumber != null && o.PhoneNumber.Length > 100) return ("owner.phoneNumber", TooLong(MessageKeys.Venue.LabelPhone, 100));
            if (v.Name == null || v.Name.Length < 3) return ("venue.name", TooShort(MessageKeys.Venue.LabelVenueName, 3));
            if (v.Name.Length > 250) return ("venue.name", TooLong(MessageKeys.Venue.LabelVenueName, 250));
            if (v.Address == null) return ("venue.address", AppError.Bad(ErrorCodes.Venue.FieldRequired, L(MessageKeys.Venue.LabelAddress)));
            if (v.Address.Length > 300) return ("venue.address", TooLong(MessageKeys.Venue.LabelAddress, 300));
            if (!VenueGeo.TryParseLocation(v.Location, out var lat, out var lon))
                return ("venue.location", AppError.Bad(ErrorCodes.Venue.LocationRequired));
            v.Location = VenueGeo.Format(lat, lon);
            if (v.City != null && v.City.Length > 60) return ("venue.city", TooLong(MessageKeys.Venue.LabelCity, 60));
            if (v.District != null && v.District.Length > 60) return ("venue.district", TooLong(MessageKeys.Venue.LabelDistrict, 60));
            if (v.Phone != null && v.Phone.Length > 30) return ("venue.phone", TooLong(MessageKeys.Venue.LabelPhone, 30));
            if (v.Description != null && v.Description.Length > 500) return ("venue.description", TooLong(MessageKeys.Venue.LabelDescription, 500));
            if (photoCount < 1) return ("photos", AppError.Bad(ErrorCodes.Venue.PhotoRequired, 1));
            var maxOwnerPhotos = P.Int(ParameterKeys.Venue.CreateOwnerMaxPhotos);
            if (photoCount > maxOwnerPhotos) return ("photos", AppError.Bad(ErrorCodes.Venue.PhotoLimitExceeded, maxOwnerPhotos));
            return null;
        }

        public OperationResult<CreateOwnerResultDto> CreateOwner(CreateOwnerInput input, List<string> photoUrls) =>
            _venueDal.CreateOwner(input, PasswordHasher.Hash(input.Owner!.Password!), photoUrls);

        public OperationResult<bool> UpdateMembership(UpdateMembershipInput input)
        {
            if (input == null || input.OwnerId <= 0) return Bad<bool>(ErrorCodes.Venue.OwnerMustBeSelected);
            return _venueDal.UpdateMembership(input.OwnerId, input.MembershipUntil, input.Suspended);
        }

        public OperationResult<bool> AssignVenue(AssignVenueInput input)
        {
            if (input == null || input.StadiumId <= 0 || input.OwnerId <= 0) return Bad<bool>(ErrorCodes.Venue.VenueAndOwnerRequired);
            return _venueDal.AssignVenue(input.StadiumId, input.OwnerId);
        }

        public OperationResult<bool> SetListed(SetListedInput input)
        {
            if (input == null || input.StadiumId <= 0) return Bad<bool>(ErrorCodes.Venue.VenueMustBeSelected);
            return _venueDal.SetListed(input.StadiumId, input.IsListed);
        }

        public List<VenueGeoRefDto> GeoRefs() => _venueDal.GeoRefs();

        public OperationResult<int> ImportCandidate(ImportCandidateInput input)
        {
            if (input == null) return Bad<int>(ErrorCodes.Venue.CandidateEmpty);
            input.Name = Clean(input.Name);
            if (input.Name == null || input.Name.Length < 3) return Bad<int>(ErrorCodes.Validation.TooShort, L(MessageKeys.Venue.LabelVenueName), 3);
            if (input.Lat is < -90 or > 90 || input.Lon is < -180 or > 180 || (input.Lat == 0 && input.Lon == 0))
                return Bad<int>(ErrorCodes.Venue.InvalidLocation);
            input.Address = Clean(input.Address);
            input.City = Clean(input.City);
            input.District = Clean(input.District);
            input.Phone = Clean(input.Phone);
            var err = CheckLength(input.Name, 250, MessageKeys.Venue.LabelVenueName) ?? CheckLength(input.Address, 300, MessageKeys.Venue.LabelAddress)
                      ?? CheckLength(input.City, 60, MessageKeys.Venue.LabelCity) ?? CheckLength(input.District, 60, MessageKeys.Venue.LabelDistrict) ?? CheckLength(input.Phone, 30, MessageKeys.Venue.LabelPhone);
            if (err != null) return Bad<int>(err);
            return _venueDal.ImportCandidate(input);
        }
    }
}
