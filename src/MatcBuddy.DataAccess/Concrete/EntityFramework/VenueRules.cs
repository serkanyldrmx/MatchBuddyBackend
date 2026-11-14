using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatcBuddy.DataAccess.Concrete.EntityFramework
{
    /// <summary>
    /// Tesis görünürlüğü, üyelik, saha kilidi ve müsaitlik kontrolleri (docs/venues-api.md).
    /// Saf fiyat / saat mantığı VenuePricing'de; burası veritabanından yükleyip onu çağırır.
    /// </summary>
    public static class VenueRules
    {
        public const string SlotTaken = ErrorCodes.Venue.SlotTaken;
        public const string MembershipInactive = ErrorCodes.Venue.MembershipInactive;
        public const string ChooseSlot = ErrorCodes.Venue.ChooseSlot;
        public const string VenueNotFound = ErrorCodes.Venue.NotFound;
        public const string PitchNotFound = ErrorCodes.Venue.PitchNotFound;
        public const string NotYourVenue = ErrorCodes.Venue.NotYourVenue;

        public static bool MembershipActive(Player? owner, DateTime now) =>
            owner != null && owner.Role == PlayerRoles.Owner && !owner.IsDeleted && !owner.MembershipSuspended
            && (owner.MembershipUntil == null || owner.MembershipUntil > now);

        /// <summary>Oyunculara listelenen tesislerin id'leri (IsListed, aktif işletme, foto, aktif saha, konum ve adres).</summary>
        public static HashSet<int> VisibleStadiumIds(MatcBuddyContext context, IEnumerable<int>? onlyIds = null)
        {
            var now = DateTime.Now;
            var query = from s in context.Stadiums.AsNoTracking()
                        join o in context.Players.AsNoTracking() on s.OwnerPlayerId equals o.PlayerId
                        where s.IsListed
                              && o.Role == PlayerRoles.Owner && !o.IsDeleted && !o.MembershipSuspended
                              && (o.MembershipUntil == null || o.MembershipUntil > now)
                              && s.Location != null && s.Location != ""
                              && s.Address != null && s.Address != ""
                              && context.StadiumPhotos.Any(ph => ph.StadiumId == s.StadiumId)
                              && context.Pitches.Any(p => p.StadiumId == s.StadiumId && p.IsActive)
                        select s.StadiumId;
            if (onlyIds != null)
            {
                var ids = onlyIds.Distinct().ToList();
                query = query.Where(id => ids.Contains(id));
            }
            return query.ToHashSet();
        }

        public static bool IsVisible(MatcBuddyContext context, int stadiumId) =>
            VisibleStadiumIds(context, new[] { stadiumId }).Contains(stadiumId);

        /// <summary>Tesisi görünmez kılan eksikler: photo, pitch, location, address, listed, membership.</summary>
        public static List<string> Missing(Stadium s, Player? owner, bool hasPhoto, bool hasActivePitch, DateTime now)
        {
            var missing = new List<string>();
            if (!hasPhoto) missing.Add("photo");
            if (!hasActivePitch) missing.Add("pitch");
            if (!VenueGeo.TryParseLocation(s.Location, out _, out _)) missing.Add("location");
            if (string.IsNullOrWhiteSpace(s.Address)) missing.Add("address");
            if (!s.IsListed) missing.Add("listed");
            if (!MembershipActive(owner, now)) missing.Add("membership");
            return missing;
        }

        /// <summary>Saha satırını transaction boyunca kilitler (aynı sahaya eşzamanlı rezervasyonlar sırayla çalışır).</summary>
        public static Pitch? LockPitch(MatcBuddyContext context, int pitchId)
        {
            return context.Pitches
                .FromSqlInterpolated($"SELECT * FROM Pitches WITH (UPDLOCK, HOLDLOCK) WHERE PitchId = {pitchId}")
                .AsEnumerable()
                .FirstOrDefault();
        }

        // ------------------------------------------------------------------ Fiyat bağlamı

        public sealed class PricingContext
        {
            public Stadium Stadium { get; init; } = null!;
            public List<VenuePricing.DayHours> Week { get; init; } = new();
            public List<PriceRule> Rules { get; init; } = new();
            public List<Campaign> Campaigns { get; init; } = new();
        }

        public static PricingContext LoadPricing(MatcBuddyContext context, Stadium stadium)
        {
            var hours = context.OpeningHours.AsNoTracking().Where(h => h.StadiumId == stadium.StadiumId).ToList();
            return new PricingContext
            {
                Stadium = stadium,
                Week = VenuePricing.WeekHours(hours, stadium.OpeningTime, stadium.ClosingTime),
                Rules = context.PriceRules.AsNoTracking().Where(r => r.StadiumId == stadium.StadiumId).ToList(),
                Campaigns = context.Campaigns.AsNoTracking().Where(c => c.StadiumId == stadium.StadiumId && c.IsActive).ToList(),
            };
        }

        public static List<OpeningHourDto> ToDtos(IEnumerable<VenuePricing.DayHours> week) =>
            week.Select(d => new OpeningHourDto
            {
                DayOfWeek = d.DayOfWeek,
                IsClosed = d.IsClosed,
                OpenTime = VenuePricing.FormatTime(d.Open),
                CloseTime = VenuePricing.FormatTime(d.Close),
            }).ToList();

        // ------------------------------------------------------------------ Doluluk

        public sealed record BusyMatch(int MatchId, int PitchId, DateTime Start, DateTime End, byte Status);

        /// <summary>Verilen sahalarda [from, to) ile çakışan bekleyen/onaylı maçlar.</summary>
        public static List<BusyMatch> BusyMatches(MatcBuddyContext context, ICollection<int> pitchIds, DateTime from, DateTime to, int? exceptMatchId = null)
        {
            // En uzun maç 120 dk: başlangıcı from - 2 saatten sonra olanlar yeterli
            var lower = from.AddHours(-2);
            return context.Matchs.AsNoTracking()
                .Where(m => m.PitchId != null && pitchIds.Contains(m.PitchId.Value)
                            && (m.IsActive == MatchStatus.Pending || m.IsActive == MatchStatus.Approved)
                            && m.MatchDate >= lower && m.MatchDate < to
                            && (exceptMatchId == null || m.MatchId != exceptMatchId))
                .Select(m => new { m.MatchId, PitchId = m.PitchId!.Value, m.MatchDate, m.DurationMinutes, m.IsActive })
                .ToList()
                .Select(m => new BusyMatch(m.MatchId, m.PitchId, m.MatchDate!.Value,
                    m.MatchDate!.Value.AddMinutes(m.DurationMinutes <= 0 ? 60 : m.DurationMinutes), m.IsActive))
                .Where(m => VenuePricing.Overlaps(m.Start, m.End, from, to))
                .ToList();
        }

        public static List<SlotBlock> Blocks(MatcBuddyContext context, ICollection<int> pitchIds, DateTime from, DateTime to) =>
            context.SlotBlocks.AsNoTracking()
                .Where(b => pitchIds.Contains(b.PitchId) && b.StartAt < to && from < b.EndAt)
                .ToList();

        /// <summary>
        /// [start, start + duration) için müsaitlik kontrolü. null = müsait, aksi halde sebep (hata kodu).
        /// (dakika, süre, geçmiş, açılış saatleri, aynı sahada bekleyen/onaylı maç ya da blok)
        /// </summary>
        public static AppError? CheckSlot(MatcBuddyContext context, PricingContext pricing, Pitch pitch, DateTime start, int durationMinutes)
        {
            var step = P.Int(ParameterKeys.Venue.SlotStepMinutes);
            if (start.Second != 0 || start.Millisecond != 0 || (start.Hour * 60 + start.Minute) % step != 0)
                return AppError.Bad(ErrorCodes.Venue.InvalidStartMinute, step);
            if (!VenuePricing.AllowedDurations.Contains(durationMinutes))
                return AppError.Bad(ErrorCodes.Venue.InvalidDuration, Msg.JoinOr(VenuePricing.AllowedDurations));
            var now = DateTime.Now;
            if (start <= now)
                return AppError.Bad(ErrorCodes.Venue.PastSlot);
            // Tesis ayarları (VenueSettings; boşsa booking.* parametreleri)
            var settings = EffectiveSettings(context, pitch.StadiumId);
            if (start < now.AddHours(settings.MinBookingNoticeHours))
                return AppError.Bad(ErrorCodes.VenueSettings.BookingTooSoon, settings.MinBookingNoticeHours);
            if (start.Date > now.Date.AddDays(settings.MaxAdvanceBookingDays))
                return AppError.Bad(ErrorCodes.VenueSettings.BookingTooFarAhead, settings.MaxAdvanceBookingDays);
            var end = start.AddMinutes(durationMinutes);
            if (!VenuePricing.IsWithinOpening(start, end, pricing.Week))
                return AppError.Bad(ErrorCodes.Venue.ClosedAtTime);
            var ids = new[] { pitch.PitchId };
            if (BusyMatches(context, ids, start, end).Count > 0 || Blocks(context, ids, start, end).Count > 0)
                return AppError.Bad(SlotTaken);
            return null;
        }

        /// <summary>Sistem varsayılanları (booking.* parametreleri).</summary>
        public static VenueSettingsValuesDto DefaultSettings() => new()
        {
            MinBookingNoticeHours = P.Int(ParameterKeys.Booking.DefaultMinNoticeHours),
            FreeCancellationHours = P.Int(ParameterKeys.Booking.DefaultFreeCancellationHours),
            MaxAdvanceBookingDays = P.Int(ParameterKeys.Booking.DefaultMaxAdvanceDays),
        };

        /// <summary>Tesis için geçerli ayarlar: tesis değeri, yoksa sistem varsayılanı.</summary>
        public static VenueSettingsValuesDto EffectiveSettings(MatcBuddyContext context, int stadiumId)
        {
            var d = DefaultSettings();
            var s = context.VenueSettings.AsNoTracking().FirstOrDefault(x => x.StadiumId == stadiumId);
            if (s == null) return d;
            return new VenueSettingsValuesDto
            {
                MinBookingNoticeHours = s.MinBookingNoticeHours ?? d.MinBookingNoticeHours,
                FreeCancellationHours = s.FreeCancellationHours ?? d.FreeCancellationHours,
                MaxAdvanceBookingDays = s.MaxAdvanceBookingDays ?? d.MaxAdvanceBookingDays,
            };
        }

        /// <summary>Teklif (müsaitlik + satırlar + ekstralar + kişi başı).</summary>
        public static QuoteDto BuildQuote(MatcBuddyContext context, PricingContext pricing, Pitch pitch, DateTime start,
            int durationMinutes, int userCount, int cleatCount, bool cameraRequested)
        {
            var reason = CheckSlot(context, pricing, pitch, start, durationMinutes);
            reason ??= CheckExtras(pricing.Stadium, cleatCount, cameraRequested);
            var quote = new QuoteDto { Available = reason == null, Reason = reason?.Message };
            if (!VenuePricing.AllowedDurations.Contains(durationMinutes)) return quote;

            var lines = VenuePricing.Lines(start, durationMinutes, pitch, pricing.Rules, pricing.Campaigns);
            quote.Lines = lines.Select(l => new QuoteLineDto
            {
                Label = l.Label, Price = l.Price, OriginalPrice = l.OriginalPrice, CampaignTitle = l.CampaignTitle,
            }).ToList();
            quote.PitchTotal = VenuePricing.Round2(lines.Sum(l => l.Price));
            quote.ExtrasTotal = VenuePricing.Extras(pricing.Stadium, Math.Max(0, cleatCount), cameraRequested);
            quote.Total = quote.PitchTotal + quote.ExtrasTotal;
            // Krampon ücreti herkese bölünmez: kişi başı payı saha + kamera üzerinden, kiralayan ayrıca öder
            quote.CleatTotal = VenuePricing.CleatTotal(pricing.Stadium, Math.Max(0, cleatCount));
            quote.CleatPrice = pricing.Stadium.CleatRental ? pricing.Stadium.CleatRentalPrice : null;
            quote.PerPlayer = VenuePricing.PerPlayer(quote.Total - quote.CleatTotal, userCount);
            return quote;
        }

        public static AppError? CheckExtras(Stadium stadium, int cleatCount, bool cameraRequested)
        {
            if (cleatCount < 0) return AppError.Bad(ErrorCodes.Validation.NegativeNotAllowed, Msg.Get(MessageKeys.Venue.LabelCleatCount));
            if (cleatCount > 0 && !stadium.CleatRental) return AppError.Bad(ErrorCodes.Venue.NoCleatRental);
            if (cameraRequested && !stadium.HasCamera) return AppError.Bad(ErrorCodes.Venue.NoCamera);
            return null;
        }

        // ------------------------------------------------------------------ Maç DTO eklentileri

        /// <summary>Maç listeleri için saha / tesis / kapak / takım adları.</summary>
        public sealed class MatchVenueLookup
        {
            public Dictionary<int, Pitch> Pitches { get; init; } = new();
            public Dictionary<int, Stadium> Stadiums { get; init; } = new();
            public Dictionary<int, string> Covers { get; init; } = new();
            public Dictionary<int, string> Teams { get; init; } = new();

            public static MatchVenueLookup Load(MatcBuddyContext context, IEnumerable<Match> matches)
            {
                var list = matches.ToList();
                var pitchIds = list.Where(m => m.PitchId.HasValue).Select(m => m.PitchId!.Value).Distinct().ToList();
                var pitches = context.Pitches.AsNoTracking().Where(p => pitchIds.Contains(p.PitchId)).ToList().ToDictionary(p => p.PitchId);
                var stadiumIds = list.Select(m => m.StadiumId).Distinct().ToList();
                var stadiums = context.Stadiums.AsNoTracking().Where(s => stadiumIds.Contains(s.StadiumId)).ToList().ToDictionary(s => s.StadiumId);
                var covers = CoverUrls(context, stadiumIds);
                var teamIds = list.SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId }).Where(t => t.HasValue).Select(t => t!.Value).Distinct().ToList();
                var teams = context.Teams.AsNoTracking().Where(t => teamIds.Contains(t.TeamId))
                    .Select(t => new { t.TeamId, t.TeamName }).ToList().ToDictionary(t => t.TeamId, t => t.TeamName);
                return new MatchVenueLookup { Pitches = pitches, Stadiums = stadiums, Covers = covers, Teams = teams };
            }

            public void Fill(MatchVenueFieldsDto dto, Match m)
            {
                dto.DurationMinutes = m.DurationMinutes <= 0 ? 60 : m.DurationMinutes;
                dto.PitchPrice = m.PitchPrice;
                dto.ExtrasPrice = m.ExtrasPrice;
                dto.TotalPrice = m.TotalPrice;
                dto.CreatedAt = m.CreatedAt == default ? null : m.CreatedAt;
                Stadiums.TryGetValue(m.StadiumId, out var matchStadium);
                // Krampon payı herkese bölünmez (kiralayan öder); geçmiş fiyat değişse de ekstra toplamını aşmasın
                var cleatTotal = Math.Min(VenuePricing.CleatTotal(matchStadium, m.CleatCount), m.ExtrasPrice ?? 0m);
                dto.PerPlayer = m.TotalPrice.HasValue ? VenuePricing.PerPlayer(m.TotalPrice.Value - cleatTotal, m.UserCount) : null;
                dto.CleatPrice = cleatTotal > 0 ? VenuePricing.Round2(cleatTotal / m.CleatCount) : null;
                dto.CleatCount = m.CleatCount;
                dto.CameraRequested = m.CameraRequested;
                dto.LookingForOpponent = m.LookingForOpponent;
                dto.RejectReason = m.RejectReason;
                dto.HomeTeam = m.HomeTeamId.HasValue && Teams.TryGetValue(m.HomeTeamId.Value, out var ht)
                    ? new TeamRefDto { TeamId = m.HomeTeamId.Value, TeamName = ht } : null;
                dto.AwayTeam = m.AwayTeamId.HasValue && Teams.TryGetValue(m.AwayTeamId.Value, out var at)
                    ? new TeamRefDto { TeamId = m.AwayTeamId.Value, TeamName = at } : null;
                if (m.PitchId.HasValue && Pitches.TryGetValue(m.PitchId.Value, out var pitch)
                    && Stadiums.TryGetValue(pitch.StadiumId, out var stadium))
                {
                    dto.Venue = new MatchVenueDto
                    {
                        StadiumId = stadium.StadiumId,
                        Name = stadium.StadiumName,
                        PitchId = pitch.PitchId,
                        PitchName = pitch.Name,
                        IsIndoor = pitch.IsIndoor,
                        Surface = pitch.Surface,
                        CoverPhotoUrl = Covers.TryGetValue(stadium.StadiumId, out var c) ? c : null,
                        CancellationPolicy = stadium.CancellationPolicy,
                        Address = stadium.Address,
                        City = stadium.City,
                        District = stadium.District,
                        Location = stadium.Location,
                    };
                }
                else if (matchStadium is { IsExternal: true })
                {
                    // Harici saha: tesis sayfası yok, oyuncu telefonla ayarladı (docs/external-venues-api.md)
                    dto.Venue = new MatchVenueDto
                    {
                        StadiumId = matchStadium.StadiumId,
                        Name = matchStadium.StadiumName,
                        IsExternal = true,
                        Address = matchStadium.Address,
                        City = matchStadium.City,
                        District = matchStadium.District,
                        Phone = matchStadium.Phone,
                        Location = matchStadium.Location,
                    };
                }
            }
        }

        /// <summary>Tesis başına kapak fotoğrafı (kapak yoksa ilk fotoğraf).</summary>
        public static Dictionary<int, string> CoverUrls(MatcBuddyContext context, ICollection<int> stadiumIds) =>
            context.StadiumPhotos.AsNoTracking()
                .Where(p => stadiumIds.Contains(p.StadiumId))
                .ToList()
                .GroupBy(p => p.StadiumId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.IsCover).ThenBy(p => p.SortOrder).ThenBy(p => p.PhotoId).First().Url);

        /// <summary>
        /// Rezervasyon ilişkisi: biri işletme, diğeri o işletmenin tesislerinden birinde son 30 gün ya da gelecekteki,
        /// bekleyen/onaylı bir maçın kurucusu ya da Going oyuncusu.
        /// </summary>
        public static bool HasReservationRelationship(MatcBuddyContext context, int a, int b)
        {
            var roles = context.Players.AsNoTracking()
                .Where(p => p.PlayerId == a || p.PlayerId == b)
                .Select(p => new { p.PlayerId, p.Role })
                .ToList();
            var ownerId = roles.FirstOrDefault(r => r.Role == PlayerRoles.Owner)?.PlayerId;
            if (ownerId == null) return false;
            var other = ownerId == a ? b : a;
            if (other == ownerId) return false;

            var since = DateTime.Now.AddDays(-30);
            var matchIds = (from m in context.Matchs.AsNoTracking()
                            join s in context.Stadiums.AsNoTracking() on m.StadiumId equals s.StadiumId
                            where s.OwnerPlayerId == ownerId
                                  && (m.IsActive == MatchStatus.Pending || m.IsActive == MatchStatus.Approved)
                                  && m.MatchDate >= since
                            select new { m.MatchId, m.CreatedByPlayerId });
            return matchIds.Any(m => m.CreatedByPlayerId == other)
                   || (from m in matchIds
                       join p in context.MatchParticipants.AsNoTracking() on m.MatchId equals p.MatchId
                       where p.PlayerId == other && p.Status == ParticipantStatus.Going
                       select p.MatchParticipantId).Any();
        }
    }
}
