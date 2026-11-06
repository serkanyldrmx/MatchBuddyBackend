using MatchBuddy.Business.Abstract;
using MatchBuddy.Business.Constants;
using MatchBuddy.Business.Security;
using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.DataAccess.Concrete;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Business.Concrete
{
    /// <summary>Girdi doğrulaması; iş kuralları (sahiplik, üyelik, müsaitlik, fiyat) EFVenueDal'da.</summary>
    public class VenueManager : IVenueService
    {
        private const decimal MaxPrice = 100000m;
        private readonly IVenueDal _venueDal;

        public VenueManager(IVenueDal venueDal)
        {
            _venueDal = venueDal;
        }

        private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private static OperationResult<T> Bad<T>(string message) => OperationResult<T>.BadRequest(message);

        private static string? CheckLength(string? value, int max, string label) =>
            value != null && value.Length > max ? $"{label} en fazla {max} karakter olabilir." : null;

        private static string? CheckPrice(decimal? price, string label, bool required = false)
        {
            if (price == null) return required ? $"{label} girilmelidir." : null;
            if (price < 0 || price > MaxPrice) return $"{label} 0 ile {MaxPrice:0} arasında olmalıdır.";
            return null;
        }

        private static string? CheckMaskAndHours(int daysMask, int fromHour, int toHour)
        {
            if (daysMask < 1 || daysMask > 127) return "En az bir gün seçilmelidir (daysMask 1–127).";
            if (fromHour < 0 || fromHour > 23 || toHour < 1 || toHour > 24 || fromHour >= toHour)
                return "Saat aralığı geçersiz (fromHour 0–23, toHour 1–24, fromHour < toHour).";
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
            if (input == null || input.PitchId <= 0) return Bad<QuoteDto>("Lütfen bir saha seçin.");
            return _venueDal.Quote(input);
        }

        public OperationResult<bool> Review(int me, ReviewInput input)
        {
            if (input == null) return Bad<bool>("Değerlendirme boş olamaz.");
            if (input.Rating < 1 || input.Rating > 5) return Bad<bool>("Puan 1 ile 5 arasında olmalıdır.");
            var comment = Clean(input.Comment);
            var err = CheckLength(comment, 500, "Yorum");
            if (err != null) return Bad<bool>(err);
            return _venueDal.Review(me, input.MatchId, (byte)input.Rating, comment);
        }

        // ------------------------------------------------------------------ Maç

        public OperationResult<int> CreateReservation(int creatorId, SaveMatchInput input)
        {
            if (input == null || input.PitchId is not > 0 || input.MatchDate == null)
                return Bad<int>("Lütfen bir saha ve saat seçin.");
            input.MatchName = Clean(input.MatchName);
            if (input.MatchName == null || input.MatchName.Length < 5) return Bad<int>(Messages.MatchNameInvalid);
            var err = CheckLength(input.MatchName, 100, "Maç adı") ?? CheckLength(input.Description, 500, "Açıklama");
            if (err != null) return Bad<int>(err);
            if (input.DurationMinutes.HasValue && !VenuePricing.AllowedDurations.Contains(input.DurationMinutes.Value))
                return Bad<int>("Maç süresi 60, 90 veya 120 dakika olmalıdır.");
            if (input.CleatCount < 0) return Bad<int>("Krampon sayısı negatif olamaz.");
            return _venueDal.CreateReservation(creatorId, input);
        }

        public OperationResult<bool> ClaimOpponent(int me, ClaimOpponentInput input)
        {
            if (input == null || input.MatchId <= 0 || input.TeamId <= 0) return Bad<bool>("Maç ve takım seçilmelidir.");
            return _venueDal.ClaimOpponent(me, input.MatchId, input.TeamId);
        }

        public OperationResult<bool> RespondReservation(int ownerId, RespondReservationInput input)
        {
            if (input == null || input.MatchId <= 0) return Bad<bool>("Rezervasyon seçilmelidir.");
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
            if (end <= start) return Bad<OwnerCalendarDto>("Bitiş tarihi başlangıçtan sonra olmalıdır.");
            if ((end - start).TotalDays > 62) return Bad<OwnerCalendarDto>("Takvim aralığı en fazla 62 gün olabilir.");
            return _venueDal.Calendar(me, stadiumId, start, end);
        }

        public OperationResult<bool> UpdateVenue(int me, VenueUpdateInput input)
        {
            if (input == null) return Bad<bool>("Tesis bilgileri boş olamaz.");
            input.Name = Clean(input.Name);
            input.Address = Clean(input.Address);
            input.Location = Clean(input.Location);
            input.Description = Clean(input.Description);
            input.Phone = Clean(input.Phone);
            input.City = Clean(input.City);
            input.District = Clean(input.District);
            input.CancellationPolicy = Clean(input.CancellationPolicy);
            if (input.Name == null || input.Name.Length < 3) return Bad<bool>("Tesis adı en az 3 karakter olmalıdır.");
            if (input.Address == null) return Bad<bool>("Adres boş olamaz.");
            if (!VenueGeo.TryParseLocation(input.Location, out var lat, out var lon))
                return Bad<bool>("Konum \"enlem, boylam\" biçiminde olmalıdır (ör. 41.0, 29.0).");
            input.Location = VenueGeo.Format(lat, lon);
            var err = CheckLength(input.Name, 250, "Tesis adı") ?? CheckLength(input.Address, 300, "Adres")
                      ?? CheckLength(input.Description, 500, "Açıklama") ?? CheckLength(input.Phone, 30, "Telefon")
                      ?? CheckLength(input.City, 60, "İl") ?? CheckLength(input.District, 60, "İlçe")
                      ?? CheckLength(input.CancellationPolicy, 500, "İptal koşulları")
                      ?? CheckPrice(input.CleatRentalPrice, "Krampon kiralama ücreti", input.CleatRental)
                      ?? CheckPrice(input.CameraPrice, "Maç kaydı ücreti", input.HasCamera);
            if (err != null) return Bad<bool>(err);
            return _venueDal.UpdateVenue(me, input);
        }

        public OperationResult<List<VenuePhotoDto>> Photos(int me, int stadiumId) => _venueDal.Photos(me, stadiumId);
        public OperationResult<bool> CheckCanAddPhoto(int me, int stadiumId) => _venueDal.CheckCanAddPhoto(me, stadiumId);
        public OperationResult<VenuePhotoDto> AddPhoto(int me, int stadiumId, string url) => _venueDal.AddPhoto(me, stadiumId, url);
        public OperationResult<string> DeletePhoto(int me, int photoId) => _venueDal.DeletePhoto(me, photoId);
        public OperationResult<bool> SetCover(int me, int photoId) => _venueDal.SetCover(me, photoId);

        public OperationResult<List<PitchDto>> Pitches(int me, int stadiumId) => _venueDal.Pitches(me, stadiumId);

        public OperationResult<int> SavePitch(int me, PitchSaveInput input)
        {
            if (input == null) return Bad<int>("Saha bilgileri boş olamaz.");
            input.Name = Clean(input.Name);
            if (input.Name == null) return Bad<int>("Saha adı boş olamaz.");
            var err = CheckLength(input.Name, 60, "Saha adı");
            if (err != null) return Bad<int>(err);
            if (input.Surface < 1 || input.Surface > 4) return Bad<int>("Zemin 1 (suni çim), 2 (doğal çim), 3 (parke) veya 4 (diğer) olmalıdır.");
            if (input.PlayersPerSide < 5 || input.PlayersPerSide > 11) return Bad<int>("Takım başına oyuncu sayısı 5 ile 11 arasında olmalıdır.");
            if (input.BasePrice <= 0 || input.BasePrice > MaxPrice) return Bad<int>($"Saatlik ücret 0'dan büyük ve en fazla {MaxPrice:0} olmalıdır.");
            return _venueDal.SavePitch(me, input);
        }

        public OperationResult<bool> DeletePitch(int me, int pitchId) => _venueDal.DeletePitch(me, pitchId);

        public OperationResult<List<OpeningHourDto>> OpeningHours(int me, int stadiumId) => _venueDal.OpeningHours(me, stadiumId);

        public OperationResult<bool> SaveOpeningHours(int me, OpeningHoursInput input)
        {
            if (input?.Days == null || input.Days.Count != 7) return Bad<bool>("7 günün açılış saatleri gönderilmelidir.");
            if (input.Days.Select(d => d.DayOfWeek).Distinct().Count() != 7 || input.Days.Any(d => d.DayOfWeek < 0 || d.DayOfWeek > 6))
                return Bad<bool>("dayOfWeek 0 (Pazartesi) … 6 (Pazar) arasında ve her gün bir kez olmalıdır.");
            var days = new List<(int, bool, TimeSpan, TimeSpan)>();
            foreach (var d in input.Days.OrderBy(d => d.DayOfWeek))
            {
                TimeSpan open = VenuePricing.DefaultOpen, close = VenuePricing.DefaultClose;
                var hasOpen = VenuePricing.TryParseTime(d.OpenTime, out var o);
                var hasClose = VenuePricing.TryParseTime(d.CloseTime, out var c);
                if (!d.IsClosed && (!hasOpen || !hasClose))
                    return Bad<bool>("Açılış ve kapanış saatleri \"HH:mm\" biçiminde olmalıdır.");
                if (hasOpen) open = o;
                if (hasClose) close = c;
                days.Add((d.DayOfWeek, d.IsClosed, open, close));
            }
            return _venueDal.SaveOpeningHours(me, input.StadiumId, days);
        }

        public OperationResult<List<PriceRuleDto>> PriceRules(int me, int stadiumId) => _venueDal.PriceRules(me, stadiumId);

        public OperationResult<int> SavePriceRule(int me, PriceRuleDto input)
        {
            if (input == null) return Bad<int>("Fiyat kuralı boş olamaz.");
            var err = CheckMaskAndHours(input.DaysMask, input.FromHour, input.ToHour);
            if (err != null) return Bad<int>(err);
            if (input.PricePerHour <= 0 || input.PricePerHour > MaxPrice) return Bad<int>($"Saatlik ücret 0'dan büyük ve en fazla {MaxPrice:0} olmalıdır.");
            return _venueDal.SavePriceRule(me, input);
        }

        public OperationResult<bool> DeletePriceRule(int me, int priceRuleId) => _venueDal.DeletePriceRule(me, priceRuleId);

        public OperationResult<List<CampaignDto>> Campaigns(int me, int stadiumId) => _venueDal.Campaigns(me, stadiumId);

        public OperationResult<int> SaveCampaign(int me, CampaignSaveInput input)
        {
            if (input == null) return Bad<int>("Kampanya boş olamaz.");
            input.Title = Clean(input.Title);
            input.Description = Clean(input.Description);
            if (input.Title == null) return Bad<int>("Kampanya başlığı boş olamaz.");
            var err = CheckLength(input.Title, 80, "Başlık") ?? CheckLength(input.Description, 300, "Açıklama")
                      ?? CheckMaskAndHours(input.DaysMask, input.FromHour, input.ToHour);
            if (err != null) return Bad<int>(err);
            if (input.StartDate == null || input.EndDate == null) return Bad<int>("Başlangıç ve bitiş tarihi gerekli.");
            if (input.EndDate.Value.Date < input.StartDate.Value.Date) return Bad<int>("Bitiş tarihi başlangıçtan önce olamaz.");
            if (input.DiscountPercent.HasValue == input.FixedPrice.HasValue)
                return Bad<int>("İndirim yüzdesi ya da sabit fiyattan tam olarak biri girilmelidir.");
            if (input.DiscountPercent is < 1 or > 90) return Bad<int>("İndirim yüzdesi 1 ile 90 arasında olmalıdır.");
            if (input.FixedPrice.HasValue && (input.FixedPrice <= 0 || input.FixedPrice > MaxPrice))
                return Bad<int>($"Sabit fiyat 0'dan büyük ve en fazla {MaxPrice:0} olmalıdır.");
            return _venueDal.SaveCampaign(me, input);
        }

        public OperationResult<bool> DeleteCampaign(int me, int campaignId) => _venueDal.DeleteCampaign(me, campaignId);

        public OperationResult<List<SlotBlockDto>> Blocks(int me, int stadiumId, DateTime? from, DateTime? to)
        {
            var start = from ?? DateTime.Today;
            var end = to ?? start.AddDays(31);
            if (end <= start) return Bad<List<SlotBlockDto>>("Bitiş tarihi başlangıçtan sonra olmalıdır.");
            return _venueDal.Blocks(me, stadiumId, start, end);
        }

        public OperationResult<int> SaveBlock(int me, BlockSaveInput input)
        {
            if (input == null || input.PitchId <= 0) return Bad<int>("Saha seçilmelidir.");
            if (input.Kind != SlotBlockKind.Closed && input.Kind != SlotBlockKind.Manual)
                return Bad<int>("Geçersiz tür (0 = kapalı/bakım, 1 = harici rezervasyon).");
            if (input.StartAt == null || input.EndAt == null) return Bad<int>("Başlangıç ve bitiş zamanı gerekli.");
            if (input.EndAt <= input.StartAt) return Bad<int>("Bitiş zamanı başlangıçtan sonra olmalıdır.");
            if ((input.EndAt.Value - input.StartAt.Value).TotalDays > 31) return Bad<int>("Tek blok en fazla 31 gün olabilir.");
            input.Reason = Clean(input.Reason);
            input.CustomerName = Clean(input.CustomerName);
            input.CustomerPhone = Clean(input.CustomerPhone);
            input.Note = Clean(input.Note);
            if (input.Kind == SlotBlockKind.Manual)
            {
                if (input.CustomerName == null) return Bad<int>("Müşteri adı girilmelidir.");
                if (input.Price == null) return Bad<int>("Ücret girilmelidir.");
                if (input.Price < 0 || input.Price > MaxPrice) return Bad<int>($"Ücret 0 ile {MaxPrice:0} arasında olmalıdır.");
            }
            else
            {
                // Kapalı blokta sebep zorunlu; boşsa eski davranıştaki gibi "Kapalı" yazılır
                input.Reason ??= "Kapalı";
            }
            var err = CheckLength(input.Reason, 100, "Sebep") ?? CheckLength(input.CustomerName, 80, "Müşteri adı")
                      ?? CheckLength(input.CustomerPhone, 30, "Telefon") ?? CheckLength(input.Note, 200, "Not");
            if (err != null) return Bad<int>(err);
            return _venueDal.SaveBlock(me, input);
        }

        public OperationResult<bool> DeleteBlock(int me, int slotBlockId) => _venueDal.DeleteBlock(me, slotBlockId);

        public OperationResult<bool> SetBlockPaid(int me, BlockPaidInput input)
        {
            if (input == null || input.SlotBlockId <= 0) return Bad<bool>("Rezervasyon seçilmelidir.");
            return _venueDal.SetBlockPaid(me, input.SlotBlockId, input.IsPaid);
        }

        public OperationResult<OwnerQuoteDto> OwnerQuote(int me, OwnerQuoteInput input)
        {
            if (input == null || input.PitchId <= 0) return Bad<OwnerQuoteDto>("Lütfen bir saha seçin.");
            if (input.Start == null) return Bad<OwnerQuoteDto>("Başlangıç zamanı gerekli.");
            if (input.DurationMinutes < 15 || input.DurationMinutes > 24 * 60)
                return Bad<OwnerQuoteDto>("Süre 15 ile 1440 dakika arasında olmalıdır.");
            return _venueDal.OwnerQuote(me, input.PitchId, input.Start.Value, input.DurationMinutes);
        }

        // ------------------------------------------------------------------ Giderler / finans

        private const decimal MaxExpense = 100000000m;

        public OperationResult<List<VenueExpenseDto>> Expenses(int me, int stadiumId, DateTime? from, DateTime? to)
        {
            if (from.HasValue && to.HasValue && to.Value.Date < from.Value.Date)
                return Bad<List<VenueExpenseDto>>("Bitiş tarihi başlangıçtan önce olamaz.");
            return _venueDal.Expenses(me, stadiumId, from, to);
        }

        public OperationResult<int> SaveExpense(int me, ExpenseSaveInput input)
        {
            if (input == null) return Bad<int>("Gider bilgileri boş olamaz.");
            if (input.StadiumId <= 0 && input.ExpenseId is not > 0) return Bad<int>("Tesis seçilmelidir.");
            if (input.Date == null) return Bad<int>("Tarih girilmelidir.");
            if (input.Date.Value.Year < 2000 || input.Date.Value.Date > DateTime.Today.AddYears(1))
                return Bad<int>("Tarih geçersiz.");
            var category = Clean(input.Category);
            var canonical = ExpenseCategories.All.FirstOrDefault(c => string.Equals(c, category, StringComparison.OrdinalIgnoreCase));
            if (canonical == null) return Bad<int>($"Kategori şunlardan biri olmalıdır: {string.Join(", ", ExpenseCategories.All)}.");
            input.Category = canonical;
            if (input.Amount <= 0) return Bad<int>("Tutar 0'dan büyük olmalıdır.");
            if (input.Amount > MaxExpense) return Bad<int>($"Tutar en fazla {MaxExpense:0} olabilir.");
            if (decimal.Round(input.Amount, 2) != input.Amount) return Bad<int>("Tutar en fazla 2 ondalık basamak içerebilir.");
            input.Description = Clean(input.Description);
            var err = CheckLength(input.Description, 200, "Açıklama");
            if (err != null) return Bad<int>(err);
            return _venueDal.SaveExpense(me, input);
        }

        public OperationResult<bool> DeleteExpense(int me, int expenseId)
        {
            if (expenseId <= 0) return Bad<bool>("Gider seçilmelidir.");
            return _venueDal.DeleteExpense(me, expenseId);
        }

        public OperationResult<OwnerFinanceDto> Finance(int me, int? stadiumId, int? year, int? month)
        {
            var today = DateTime.Today;
            var y = year ?? today.Year;
            var m = month ?? today.Month;
            if (y < 2000 || y > 2100) return Bad<OwnerFinanceDto>("Yıl geçersiz.");
            if (m < 1 || m > 12) return Bad<OwnerFinanceDto>("Ay 1 ile 12 arasında olmalıdır.");
            return _venueDal.Finance(me, stadiumId, y, m);
        }

        public OperationResult<List<VenueReviewDto>> OwnerReviews(int me, int stadiumId) => _venueDal.OwnerReviews(me, stadiumId);

        public OperationResult<bool> ReplyReview(int me, ReviewReplyInput input)
        {
            var reply = Clean(input?.Reply);
            if (reply == null) return Bad<bool>("Yanıt boş olamaz.");
            var err = CheckLength(reply, 500, "Yanıt");
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
            if (!res.Success) return OperationResult<ResetOwnerPasswordResultDto>.NotFound(res.Message ?? "İşletme bulunamadı.");
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
            while (chars.Count < 12) chars.Add(all[R(all.Length)]);
            return new string(chars.OrderBy(_ => R(10000)).ToArray());
        }

        public (string Field, string Message)? ValidateCreateOwner(CreateOwnerInput? input, int photoCount)
        {
            if (input?.Owner == null) return ("owner", "İşletme hesabı bilgileri gerekli.");
            if (input.Venue == null) return ("venue", "Tesis bilgileri gerekli.");
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

            if (o.Name == null || o.Name.Length < 3) return ("owner.name", "İşletme adı en az 3 karakter olmalıdır.");
            if (o.Name.Length > 100) return ("owner.name", "İşletme adı en fazla 100 karakter olabilir.");
            if (o.ContactName != null && o.ContactName.Length > 100) return ("owner.contactName", "Yetkili adı en fazla 100 karakter olabilir.");
            if (o.UserName == null) return ("owner.userName", "Kullanıcı adı zorunludur.");
            if (o.UserName.Length > 25) return ("owner.userName", "Kullanıcı adı en fazla 25 karakter olabilir.");
            if (_venueDal.UserNameTaken(o.UserName)) return ("owner.userName", "Bu kullanıcı adı zaten kullanılıyor.");
            if (string.IsNullOrEmpty(o.Password) || o.Password.Length < 6) return ("owner.password", "Şifre en az 6 karakter olmalıdır.");
            if (o.Email != null && o.Email.Length > 100) return ("owner.email", "E-posta en fazla 100 karakter olabilir.");
            if (o.PhoneNumber != null && o.PhoneNumber.Length > 100) return ("owner.phoneNumber", "Telefon en fazla 100 karakter olabilir.");
            if (v.Name == null || v.Name.Length < 3) return ("venue.name", "Tesis adı en az 3 karakter olmalıdır.");
            if (v.Name.Length > 250) return ("venue.name", "Tesis adı en fazla 250 karakter olabilir.");
            if (v.Address == null) return ("venue.address", "Adres zorunludur.");
            if (v.Address.Length > 300) return ("venue.address", "Adres en fazla 300 karakter olabilir.");
            if (!VenueGeo.TryParseLocation(v.Location, out var lat, out var lon))
                return ("venue.location", "Konum zorunludur (\"enlem, boylam\" biçiminde, ör. 41.0, 29.0).");
            v.Location = VenueGeo.Format(lat, lon);
            if (v.City != null && v.City.Length > 60) return ("venue.city", "İl en fazla 60 karakter olabilir.");
            if (v.District != null && v.District.Length > 60) return ("venue.district", "İlçe en fazla 60 karakter olabilir.");
            if (v.Phone != null && v.Phone.Length > 30) return ("venue.phone", "Telefon en fazla 30 karakter olabilir.");
            if (v.Description != null && v.Description.Length > 500) return ("venue.description", "Açıklama en fazla 500 karakter olabilir.");
            if (photoCount < 1) return ("photos", "En az 1 fotoğraf zorunludur.");
            if (photoCount > 10) return ("photos", "En fazla 10 fotoğraf yüklenebilir.");
            return null;
        }

        public OperationResult<CreateOwnerResultDto> CreateOwner(CreateOwnerInput input, List<string> photoUrls) =>
            _venueDal.CreateOwner(input, PasswordHasher.Hash(input.Owner!.Password!), photoUrls);

        public OperationResult<bool> UpdateMembership(UpdateMembershipInput input)
        {
            if (input == null || input.OwnerId <= 0) return Bad<bool>("İşletme seçilmelidir.");
            return _venueDal.UpdateMembership(input.OwnerId, input.MembershipUntil, input.Suspended);
        }

        public OperationResult<bool> AssignVenue(AssignVenueInput input)
        {
            if (input == null || input.StadiumId <= 0 || input.OwnerId <= 0) return Bad<bool>("Tesis ve işletme seçilmelidir.");
            return _venueDal.AssignVenue(input.StadiumId, input.OwnerId);
        }

        public OperationResult<bool> SetListed(SetListedInput input)
        {
            if (input == null || input.StadiumId <= 0) return Bad<bool>("Tesis seçilmelidir.");
            return _venueDal.SetListed(input.StadiumId, input.IsListed);
        }

        public List<VenueGeoRefDto> GeoRefs() => _venueDal.GeoRefs();

        public OperationResult<int> ImportCandidate(ImportCandidateInput input)
        {
            if (input == null) return Bad<int>("Aday bilgileri boş olamaz.");
            input.Name = Clean(input.Name);
            if (input.Name == null || input.Name.Length < 3) return Bad<int>("Tesis adı en az 3 karakter olmalıdır.");
            if (input.Lat is < -90 or > 90 || input.Lon is < -180 or > 180 || (input.Lat == 0 && input.Lon == 0))
                return Bad<int>("Geçersiz konum.");
            input.Address = Clean(input.Address);
            input.City = Clean(input.City);
            input.District = Clean(input.District);
            input.Phone = Clean(input.Phone);
            var err = CheckLength(input.Name, 250, "Tesis adı") ?? CheckLength(input.Address, 300, "Adres")
                      ?? CheckLength(input.City, 60, "İl") ?? CheckLength(input.District, 60, "İlçe") ?? CheckLength(input.Phone, 30, "Telefon");
            if (err != null) return Bad<int>(err);
            return _venueDal.ImportCandidate(input);
        }
    }
}
