using System.Globalization;
using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using static MatchBuddy.DataAccess.Concrete.EntityFramework.ParticipationCore;

namespace MatchBuddy.DataAccess.Concrete.EntityFramework
{
    /// <summary>
    /// Anlaşmalı tesisler (docs/venues-api.md): oyuncu uç noktaları, maç kurma (rezervasyon), rakip takım,
    /// rezervasyon durumu. İşletme paneli EFVenueDal.Owner.cs, admin EFVenueDal.Admin.cs içinde.
    /// </summary>
    public partial class EFVenueDal : IVenueDal
    {
        private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

        private static string Norm(string? s) => (s ?? string.Empty).Trim().ToLower(Tr);

        private static VenueAmenitiesDto Amenities(Stadium s) => new()
        {
            CleatRental = s.CleatRental,
            Camera = s.HasCamera,
            Shower = s.HasShower,
            LockerRoom = s.HasLockerRoom,
            Parking = s.HasParking,
            Cafe = s.HasCafe,
            Shuttle = s.HasShuttle,
            Bibs = s.ProvidesBibs,
            Ball = s.ProvidesBall,
        };

        private static CampaignDto ToDto(Campaign c) => new()
        {
            CampaignId = c.CampaignId,
            StadiumId = c.StadiumId,
            Title = c.Title,
            Description = c.Description,
            StartDate = c.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            EndDate = c.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DaysMask = c.DaysMask,
            FromHour = c.FromHour,
            ToHour = c.ToHour,
            DiscountPercent = c.DiscountPercent,
            FixedPrice = c.FixedPrice,
            PitchId = c.PitchId,
            IsActive = c.IsActive,
        };

        private static PitchDto ToDto(Pitch p) => new()
        {
            PitchId = p.PitchId,
            StadiumId = p.StadiumId,
            Name = p.Name,
            Surface = p.Surface,
            IsIndoor = p.IsIndoor,
            PlayersPerSide = p.PlayersPerSide,
            BasePrice = p.BasePrice,
            IsActive = p.IsActive,
        };

        private static double? RoundRating(double? v) => v.HasValue ? Math.Round(v.Value, 1) : null;

        // Dilim durumu: past > booked > pending > blocked > free.
        // Harici rezervasyon (blok Kind 1) oyunculara "booked", kapalı/bakım bloğu (Kind 0) "blocked" görünür.
        private static string SlotStatus(DateTime start, DateTime end, int pitchId, List<VenueRules.BusyMatch> busy, List<SlotBlock> blocks, DateTime now)
        {
            if (start < now) return "past";
            var overlapping = busy.Where(m => m.PitchId == pitchId && VenuePricing.Overlaps(m.Start, m.End, start, end)).ToList();
            var overlappingBlocks = blocks.Where(b => b.PitchId == pitchId && VenuePricing.Overlaps(b.StartAt, b.EndAt, start, end)).ToList();
            if (overlapping.Any(m => m.Status == MatchStatus.Approved) || overlappingBlocks.Any(b => b.Kind == SlotBlockKind.Manual)) return "booked";
            if (overlapping.Count > 0) return "pending";
            if (overlappingBlocks.Count > 0) return "blocked";
            return "free";
        }

        // Görünür değilse: sadece tesisin işletmesi ve admin görebilir
        private static bool CanView(MatchBuddyContext context, Stadium stadium, int viewerId, bool isAdmin) =>
            isAdmin || stadium.OwnerPlayerId == viewerId || VenueRules.IsVisible(context, stadium.StadiumId);

        // ------------------------------------------------------------------ List

        public List<VenueListItemDto> List(string? city, string? query, DateTime date)
        {
            using var context = new MatchBuddyContext();
            var visible = VenueRules.VisibleStadiumIds(context).ToList();
            if (visible.Count == 0) return new List<VenueListItemDto>();

            var stadiums = context.Stadiums.AsNoTracking().Where(s => visible.Contains(s.StadiumId)).ToList();
            var c = Norm(city);
            if (c.Length > 0) stadiums = stadiums.Where(s => Norm(s.City).Contains(c)).ToList();
            var q = Norm(query);
            if (q.Length > 0)
            {
                stadiums = stadiums.Where(s => Norm(s.StadiumName).Contains(q) || Norm(s.District).Contains(q)
                                               || Norm(s.Address).Contains(q) || Norm(s.City).Contains(q)).ToList();
            }
            if (stadiums.Count == 0) return new List<VenueListItemDto>();

            var ids = stadiums.Select(s => s.StadiumId).ToList();
            var pitches = context.Pitches.AsNoTracking().Where(p => ids.Contains(p.StadiumId) && p.IsActive).ToList();
            var rules = context.PriceRules.AsNoTracking().Where(r => ids.Contains(r.StadiumId)).ToList();
            var today = DateTime.Today;
            var campaigns = context.Campaigns.AsNoTracking().Where(x => ids.Contains(x.StadiumId) && x.IsActive && x.EndDate >= today).ToList();
            var hours = context.OpeningHours.AsNoTracking().Where(h => ids.Contains(h.StadiumId)).ToList();
            var covers = VenueRules.CoverUrls(context, ids);
            var ratings = context.VenueReviews.AsNoTracking().Where(r => ids.Contains(r.StadiumId))
                .GroupBy(r => r.StadiumId)
                .Select(g => new { StadiumId = g.Key, Avg = g.Average(r => (double)r.Rating), Count = g.Count() })
                .ToList().ToDictionary(x => x.StadiumId);

            var day = date.Date;
            var pitchIds = pitches.Select(p => p.PitchId).ToList();
            var busy = VenueRules.BusyMatches(context, pitchIds, day, day.AddDays(2));
            var blocks = VenueRules.Blocks(context, pitchIds, day, day.AddDays(2));
            var now = DateTime.Now;

            return stadiums
                .Select(s =>
                {
                    var myPitches = pitches.Where(p => p.StadiumId == s.StadiumId).ToList();
                    var week = VenuePricing.WeekHours(hours.Where(h => h.StadiumId == s.StadiumId), s.OpeningTime, s.ClosingTime);
                    var slots = VenuePricing.DaySlots(day, week);
                    var free = myPitches.Sum(p => slots.Count(sl => SlotStatus(sl.Start, sl.End, p.PitchId, busy, blocks, now) == "free"));
                    ratings.TryGetValue(s.StadiumId, out var rating);
                    return new VenueListItemDto
                    {
                        StadiumId = s.StadiumId,
                        Name = s.StadiumName,
                        City = s.City,
                        District = s.District,
                        Address = s.Address,
                        Location = s.Location,
                        CoverPhotoUrl = covers.TryGetValue(s.StadiumId, out var cover) ? cover : null,
                        PitchCount = myPitches.Count,
                        Amenities = Amenities(s),
                        MinPrice = VenuePricing.MinPrice(myPitches, rules.Where(r => r.StadiumId == s.StadiumId)),
                        HasActiveCampaign = campaigns.Any(x => x.StadiumId == s.StadiumId),
                        Rating = RoundRating(rating?.Avg),
                        ReviewCount = rating?.Count ?? 0,
                        FreeSlotsToday = free,
                    };
                })
                .OrderByDescending(v => v.Rating ?? 0)
                .ThenBy(v => v.Name, StringComparer.Create(Tr, true))
                .ToList();
        }

        // ------------------------------------------------------------------ Detail

        public OperationResult<VenueDetailDto> Detail(int stadiumId, int viewerId, bool isAdmin)
        {
            using var context = new MatchBuddyContext();
            var s = context.Stadiums.AsNoTracking().FirstOrDefault(x => x.StadiumId == stadiumId);
            if (s == null || !CanView(context, s, viewerId, isAdmin))
                return OperationResult<VenueDetailDto>.NotFound(VenueRules.VenueNotFound);
            return OperationResult<VenueDetailDto>.Ok(BuildDetail<VenueDetailDto>(context, s, ownerView: false));
        }

        /// <summary>İşletme görünümü: oyunculara görünmese bile; pasif sahalar dahil, + isListed / missing.</summary>
        public OperationResult<OwnerVenueDetailDto> OwnerVenue(int me, int stadiumId)
        {
            using var context = new MatchBuddyContext();
            var s = context.Stadiums.AsNoTracking().FirstOrDefault(x => x.StadiumId == stadiumId);
            var g = Guard(context, me, s, false);
            if (g != null) return Fail<OwnerVenueDetailDto>(g);
            var dto = BuildDetail<OwnerVenueDetailDto>(context, s!, ownerView: true);
            var owner = context.Players.AsNoTracking().FirstOrDefault(p => p.PlayerId == me);
            dto.IsListed = s!.IsListed;
            dto.IsVisible = VenueRules.IsVisible(context, s.StadiumId);
            dto.Missing = VenueRules.Missing(s, owner, dto.Photos.Count > 0, dto.Pitches.Any(p => p.IsActive), DateTime.Now);
            return OperationResult<OwnerVenueDetailDto>.Ok(dto);
        }

        private static T BuildDetail<T>(MatchBuddyContext context, Stadium s, bool ownerView) where T : VenueDetailDto, new()
        {
            var stadiumId = s.StadiumId;
            var today = DateTime.Today;
            var allPitches = context.Pitches.AsNoTracking().Where(p => p.StadiumId == stadiumId)
                .OrderBy(p => p.Name).ThenBy(p => p.PitchId).ToList();
            var photos = context.StadiumPhotos.AsNoTracking().Where(p => p.StadiumId == stadiumId)
                .OrderByDescending(p => p.IsCover).ThenBy(p => p.SortOrder).ThenBy(p => p.PhotoId).ToList();
            var pitches = allPitches.Where(p => p.IsActive).ToList();
            var rules = context.PriceRules.AsNoTracking().Where(r => r.StadiumId == stadiumId).ToList();
            var campaigns = context.Campaigns.AsNoTracking()
                .Where(x => x.StadiumId == stadiumId && x.IsActive && x.EndDate >= today)
                .OrderBy(x => x.StartDate).ThenBy(x => x.CampaignId).ToList();
            var hours = context.OpeningHours.AsNoTracking().Where(h => h.StadiumId == stadiumId).ToList();
            var week = VenuePricing.WeekHours(hours, s.OpeningTime, s.ClosingTime);
            var owner = s.OwnerPlayerId.HasValue
                ? context.Players.AsNoTracking().FirstOrDefault(p => p.PlayerId == s.OwnerPlayerId.Value && !p.IsDeleted)
                : null;

            var allReviews = context.VenueReviews.AsNoTracking().Where(r => r.StadiumId == stadiumId).ToList();
            var last = allReviews.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.ReviewId).Take(P.Int(ParameterKeys.Venue.ReviewsShown)).ToList();
            var reviewerIds = last.Select(r => r.PlayerId).Distinct().ToList();
            var reviewers = context.Players.AsNoTracking().Where(p => reviewerIds.Contains(p.PlayerId)).ToList()
                .ToDictionary(p => p.PlayerId, PlayerSummaryDto.FromPlayer);
            var reviewMatches = ReviewMatchRefs(context, last.Select(r => r.MatchId));

            var busy = VenueRules.BusyMatches(context, pitches.Select(p => p.PitchId).ToList(), today, today.AddDays(2));
            var blocks = VenueRules.Blocks(context, pitches.Select(p => p.PitchId).ToList(), today, today.AddDays(2));
            var slots = VenuePricing.DaySlots(today, week);
            var now = DateTime.Now;

            return new T
            {
                StadiumId = s.StadiumId,
                Name = s.StadiumName,
                City = s.City,
                District = s.District,
                Address = s.Address,
                Location = s.Location,
                CoverPhotoUrl = photos.FirstOrDefault()?.Url,
                PitchCount = pitches.Count,
                Amenities = Amenities(s),
                MinPrice = VenuePricing.MinPrice(pitches, rules),
                HasActiveCampaign = campaigns.Count > 0,
                Rating = allReviews.Count > 0 ? RoundRating(allReviews.Average(r => (double)r.Rating)) : null,
                ReviewCount = allReviews.Count,
                FreeSlotsToday = pitches.Sum(p => slots.Count(sl => SlotStatus(sl.Start, sl.End, p.PitchId, busy, blocks, now) == "free")),
                Description = s.Description,
                Phone = s.Phone,
                CancellationPolicy = s.CancellationPolicy,
                Photos = photos.Select(p => new VenuePhotoDto { PhotoId = p.PhotoId, Url = p.Url, IsCover = p.IsCover, SortOrder = p.SortOrder }).ToList(),
                Pitches = (ownerView ? allPitches : pitches).Select(ToDto).ToList(),
                OpeningHours = VenueRules.ToDtos(week),
                Prices = new VenuePricesDto
                {
                    CleatRentalPrice = s.CleatRental || ownerView ? s.CleatRentalPrice : null,
                    CameraPrice = s.HasCamera || ownerView ? s.CameraPrice : null,
                },
                Campaigns = campaigns.Select(ToDto).ToList(),
                Owner = owner == null ? null : new VenueOwnerRefDto { PlayerId = owner.PlayerId, Name = owner.PlayerName },
                Reviews = last.Select(r => new VenueReviewDto
                {
                    ReviewId = r.ReviewId,
                    MatchId = r.MatchId,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt,
                    Player = reviewers.TryGetValue(r.PlayerId, out var pl) ? pl : null,
                    OwnerReply = r.OwnerReply,
                    RepliedAt = r.RepliedAt,
                    Match = reviewMatches.TryGetValue(r.MatchId, out var rm) ? rm : null,
                }).ToList(),
                RatingBreakdown = Enumerable.Range(1, 5).ToDictionary(i => i.ToString(), i => allReviews.Count(r => r.Rating == i)),
            };
        }

        private static Dictionary<int, ReviewMatchRefDto> ReviewMatchRefs(MatchBuddyContext context, IEnumerable<int> matchIds)
        {
            var ids = matchIds.Distinct().ToList();
            return context.Matchs.AsNoTracking().Where(m => ids.Contains(m.MatchId))
                .Select(m => new ReviewMatchRefDto { MatchId = m.MatchId, MatchName = m.MatchName, MatchDate = m.MatchDate })
                .ToList().ToDictionary(m => m.MatchId);
        }

        // ------------------------------------------------------------------ Availability

        public OperationResult<AvailabilityDto> Availability(int stadiumId, DateTime date, int viewerId, bool isAdmin)
        {
            using var context = new MatchBuddyContext();
            var s = context.Stadiums.AsNoTracking().FirstOrDefault(x => x.StadiumId == stadiumId);
            if (s == null || !CanView(context, s, viewerId, isAdmin))
                return OperationResult<AvailabilityDto>.NotFound(VenueRules.VenueNotFound);

            var day = date.Date;
            var pricing = VenueRules.LoadPricing(context, s);
            var pitches = context.Pitches.AsNoTracking().Where(p => p.StadiumId == stadiumId && p.IsActive)
                .OrderBy(p => p.Name).ThenBy(p => p.PitchId).ToList();
            var pitchIds = pitches.Select(p => p.PitchId).ToList();
            var busy = VenueRules.BusyMatches(context, pitchIds, day, day.AddDays(2));
            var blocks = VenueRules.Blocks(context, pitchIds, day, day.AddDays(2));
            var slots = VenuePricing.DaySlots(day, pricing.Week);
            var now = DateTime.Now;

            return OperationResult<AvailabilityDto>.Ok(new AvailabilityDto
            {
                Date = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                IsClosed = pricing.Week[VenuePricing.DayIndex(day)].IsClosed,
                Pitches = pitches.Select(p => new PitchSlotsDto
                {
                    PitchId = p.PitchId,
                    Name = p.Name,
                    Slots = slots.Select(sl =>
                    {
                        var hp = VenuePricing.PriceForHour(sl.Start, p, pricing.Rules, pricing.Campaigns);
                        return new SlotDto
                        {
                            Start = sl.Start,
                            End = sl.End,
                            Status = SlotStatus(sl.Start, sl.End, p.PitchId, busy, blocks, now),
                            Price = hp.Price,
                            OriginalPrice = hp.OriginalPrice,
                            CampaignTitle = hp.CampaignTitle,
                        };
                    }).ToList(),
                }).ToList(),
            });
        }

        // ------------------------------------------------------------------ Quote

        public OperationResult<QuoteDto> Quote(QuoteInput input)
        {
            using var context = new MatchBuddyContext();
            var pitch = context.Pitches.AsNoTracking().FirstOrDefault(p => p.PitchId == input.PitchId);
            if (pitch == null) return OperationResult<QuoteDto>.NotFound(VenueRules.PitchNotFound);
            if (input.Start == null)
                return OperationResult<QuoteDto>.Ok(new QuoteDto { Available = false, Reason = Msg.Get(VenueRules.ChooseSlot) });

            var stadium = context.Stadiums.AsNoTracking().First(s => s.StadiumId == pitch.StadiumId);
            var pricing = VenueRules.LoadPricing(context, stadium);
            var quote = VenueRules.BuildQuote(context, pricing, pitch, input.Start.Value, input.DurationMinutes,
                input.UserCount, input.CleatCount, input.CameraRequested);
            if (quote.Available && !VenueRules.IsVisible(context, stadium.StadiumId))
            {
                quote.Available = false;
                quote.Reason = Msg.Get(ErrorCodes.Reservation.VenueClosed);
            }
            else if (quote.Available && !pitch.IsActive)
            {
                quote.Available = false;
                quote.Reason = Msg.Get(ErrorCodes.Reservation.PitchUnavailable);
            }
            return OperationResult<QuoteDto>.Ok(quote);
        }

        // ------------------------------------------------------------------ Review

        public OperationResult<bool> Review(int me, int matchId, byte rating, string? comment)
        {
            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var match = context.Matchs.AsNoTracking().FirstOrDefault(m => m.MatchId == matchId);
            if (match == null) return OperationResult<bool>.NotFound();
            var stadiumId = match.PitchId.HasValue
                ? context.Pitches.AsNoTracking().Where(p => p.PitchId == match.PitchId.Value).Select(p => (int?)p.StadiumId).FirstOrDefault()
                : null;
            if (stadiumId == null)
                return OperationResult<bool>.BadRequest(ErrorCodes.Reservation.ReviewNotVenueMatch);
            if (match.IsActive == MatchStatus.Rejected)
                return OperationResult<bool>.BadRequest(ErrorCodes.Reservation.ReviewMatchRejected);
            if (!match.MatchDate.HasValue || match.MatchDate.Value.AddMinutes(match.DurationMinutes) > DateTime.Now)
                return OperationResult<bool>.BadRequest(ErrorCodes.Reservation.ReviewMatchNotPlayed);
            var going = context.MatchParticipants.Any(p => p.MatchId == matchId && p.PlayerId == me && p.Status == ParticipantStatus.Going);
            if (!going)
                return OperationResult<bool>.BadRequest(ErrorCodes.Reservation.ReviewNotParticipant);

            var existing = context.VenueReviews.FirstOrDefault(r => r.MatchId == matchId && r.PlayerId == me);
            var created = existing == null;
            if (existing == null)
            {
                context.VenueReviews.Add(new VenueReview
                {
                    StadiumId = stadiumId.Value,
                    PlayerId = me,
                    MatchId = matchId,
                    Rating = rating,
                    Comment = comment,
                    CreatedAt = DateTime.Now,
                });
            }
            else
            {
                existing.Rating = rating;
                existing.Comment = comment;
            }

            if (created)
            {
                var stadium = context.Stadiums.AsNoTracking().First(s => s.StadiumId == stadiumId.Value);
                if (stadium.OwnerPlayerId.HasValue)
                {
                    var player = context.Players.AsNoTracking().FirstOrDefault(p => p.PlayerId == me);
                    NotificationWriter.Add(context, NotificationType.VenueReview,
                        Msg.GetDefault(MessageKeys.Reservation.NotifVenueReview, DisplayName(player), stadium.StadiumName, rating),
                        matchId, stadium.StadiumId, new[] { stadium.OwnerPlayerId.Value });
                }
            }

            try
            {
                context.SaveChanges();
                transaction.Commit();
            }
            catch (DbUpdateException)
            {
                return OperationResult<bool>.BadRequest(ErrorCodes.Reservation.ReviewSaveFailed);
            }
            return OperationResult<bool>.Ok(true, Msg.Get(created ? MessageKeys.Reservation.ReviewSaved : MessageKeys.Reservation.ReviewUpdated));
        }

        // ------------------------------------------------------------------ Maç kurma (SaveMatch)

        // Takımın (silinmemiş, işletme olmayan) üyeleri, takıma katılma sırasıyla
        private static List<int> TeamMemberIds(MatchBuddyContext context, int teamId) =>
            (from pt in context.PlayerTeam
             join p in context.Players on pt.PlayerId equals p.PlayerId
             where pt.TeamId == teamId && !p.IsDeleted && p.Role != PlayerRoles.Owner
             orderby pt.PlayerTeamId
             select p.PlayerId).ToList().Distinct().ToList();

        /// <summary>
        /// Takım kadrosu kuralı (docs/venues-api.md "Ek paket" §5): takımın tüm üyeleri doğrudan maça eklenir
        /// (Going, verilen taraf, Confirmed = false). Taraf kapasitesi (ceil(UserCount / 2)) ya da toplam kapasite
        /// doluysa fazlası Waitlist olur (JoinedAt = ekleme sırası). Zaten Going/Waitlist olanlar atlanır; "gelemiyor"
        /// kaydı yeniden kullanılır. Engelleme kontrolü yapılmaz (takım üyeliği önceliklidir). MatchInvite oluşturulmaz,
        /// takım arkadaşları için JoinedMatch aktivitesi yazılmaz. Eklenen her üyeye (actor hariç) MatchJoin bildirimi
        /// (NotifyMatchUpdates tercihi) gider. SaveChanges çağırmaz. Eklenen (Going) ve yedeğe alınan id'leri döner.
        /// </summary>
        private static (List<int> Going, List<int> Waitlisted) AddTeamToSquad(MatchBuddyContext context, Match match, int actorId, Player? actor, int teamId, byte side)
        {
            var going = new List<int>();
            var waitlisted = new List<int>();
            var memberIds = TeamMemberIds(context, teamId);
            if (memberIds.Count == 0) return (going, waitlisted);

            // Veritabanındaki kayıtlar + bu context'te henüz kaydedilmemiş (kurucu gibi) kayıtlar
            context.MatchParticipants.Where(p => p.MatchId == match.MatchId).Load();
            var participants = context.MatchParticipants.Local.Where(p => p.MatchId == match.MatchId).ToList();
            var sideCapacity = SideCapacity(match);
            // Misafirler kadroya (toplam ve taraf) sayılır
            var guests = LoadGuests(context, match.MatchId);
            var now = DateTime.Now;
            var order = 0;

            foreach (var id in memberIds)
            {
                var row = participants.FirstOrDefault(p => p.PlayerId == id);
                if (row != null && (row.Status == ParticipantStatus.Going || row.Status == ParticipantStatus.Waitlist)) continue;

                var goingRows = participants.Where(p => p.Status == ParticipantStatus.Going).ToList();
                var canGo = goingRows.Count + guests.Count < match.UserCount && SideCount(goingRows, guests, side) < sideCapacity;
                if (row == null)
                {
                    row = new MatchParticipant { MatchId = match.MatchId, PlayerId = id };
                    context.MatchParticipants.Add(row);
                    participants.Add(row);
                }
                row.Status = canGo ? ParticipantStatus.Going : ParticipantStatus.Waitlist;
                row.Side = canGo ? side : null;
                row.Confirmed = false;
                row.JoinedAt = now.AddMilliseconds(order++);
                row.ReminderSentAt = null;
                row.Goals = 0;
                row.Assists = 0;
                (canGo ? going : waitlisted).Add(id);
            }

            var name = DisplayName(actor);
            var goingRecipients = going.Where(id => id != actorId).ToList();
            if (goingRecipients.Count > 0)
            {
                AddNotification(context, NotificationType.MatchJoin,
                    Msg.GetDefault(MessageKeys.Reservation.NotifTeamSquadAdded, name, match.MatchName),
                    match.MatchId, goingRecipients);
            }
            var waitRecipients = waitlisted.Where(id => id != actorId).ToList();
            if (waitRecipients.Count > 0)
            {
                AddNotification(context, NotificationType.MatchJoin,
                    Msg.GetDefault(MessageKeys.Reservation.NotifTeamSquadWaitlisted, name, match.MatchName),
                    match.MatchId, waitRecipients);
            }
            return (going, waitlisted);
        }

        public OperationResult<int> CreateReservation(int creatorId, SaveMatchInput input)
        {
            if (input.PitchId is not > 0 || input.MatchDate == null)
                return OperationResult<int>.BadRequest(VenueRules.ChooseSlot);

            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var creator = context.Players.AsNoTracking().FirstOrDefault(p => p.PlayerId == creatorId && !p.IsDeleted);
            if (creator == null) return OperationResult<int>.NotFound(ErrorCodes.Player.NotFound);
            if (creator.Role == PlayerRoles.Owner) return OperationResult<int>.Forbidden(ErrorCodes.Reservation.OwnerCannotCreate);

            // Eşzamanlı rezervasyonlar bu kilit üzerinde sıraya girer
            var pitch = VenueRules.LockPitch(context, input.PitchId.Value);
            if (pitch == null) return OperationResult<int>.NotFound(VenueRules.PitchNotFound);
            var stadium = context.Stadiums.AsNoTracking().First(s => s.StadiumId == pitch.StadiumId);
            if (!VenueRules.IsVisible(context, stadium.StadiumId))
                return OperationResult<int>.BadRequest(ErrorCodes.Reservation.VenueClosed);
            if (!pitch.IsActive)
                return OperationResult<int>.BadRequest(ErrorCodes.Reservation.PitchUnavailable);

            var duration = input.DurationMinutes ?? 60;
            var min = VenuePricing.MinUserCount();
            var max = VenuePricing.MaxUserCount(pitch.PlayersPerSide);
            if (input.UserCount < min || input.UserCount > max)
                return OperationResult<int>.BadRequest(ErrorCodes.Validation.OutOfRange, Msg.Get(MessageKeys.Reservation.LabelUserCount), min, max);
            if (input.CleatCount > input.UserCount)
                return OperationResult<int>.BadRequest(ErrorCodes.Reservation.CleatsExceedPlayers);
            var extrasError = VenueRules.CheckExtras(stadium, input.CleatCount, input.CameraRequested);
            if (extrasError != null) return OperationResult<int>.BadRequest(extrasError);

            // Takım kuralları
            Team? home = null, away = null;
            if (input.HomeTeamId.HasValue)
            {
                home = context.Teams.AsNoTracking().FirstOrDefault(t => t.TeamId == input.HomeTeamId.Value);
                if (home == null) return OperationResult<int>.BadRequest(ErrorCodes.Reservation.HomeTeamNotFound);
                if (!context.PlayerTeam.Any(pt => pt.TeamId == home.TeamId && pt.PlayerId == creatorId))
                    return OperationResult<int>.BadRequest(ErrorCodes.Reservation.NotHomeTeamMember);
            }
            if (input.AwayTeamId.HasValue)
            {
                if (input.AwayTeamId == input.HomeTeamId)
                    return OperationResult<int>.BadRequest(ErrorCodes.Reservation.SameTeams);
                away = context.Teams.AsNoTracking().FirstOrDefault(t => t.TeamId == input.AwayTeamId.Value);
                if (away == null) return OperationResult<int>.BadRequest(ErrorCodes.Reservation.AwayTeamNotFound);
            }
            if (input.LookingForOpponent && input.AwayTeamId.HasValue)
                return OperationResult<int>.BadRequest(ErrorCodes.Reservation.OpponentFlagConflict);

            var start = input.MatchDate.Value;
            var pricing = VenueRules.LoadPricing(context, stadium);
            var reason = VenueRules.CheckSlot(context, pricing, pitch, start, duration);
            if (reason != null) return OperationResult<int>.BadRequest(reason);

            var quote = VenueRules.BuildQuote(context, pricing, pitch, start, duration, input.UserCount, input.CleatCount, input.CameraRequested);
            var match = new Match
            {
                MatchName = input.MatchName!.Trim(),
                Description = input.Description?.Trim() ?? string.Empty,
                MatchDate = start,
                UserCount = input.UserCount,
                IsActive = MatchStatus.Pending,
                Likes = 0,
                StadiumId = stadium.StadiumId,
                PitchId = pitch.PitchId,
                DurationMinutes = duration,
                PitchPrice = quote.PitchTotal,
                ExtrasPrice = quote.ExtrasTotal,
                TotalPrice = quote.Total,
                CleatCount = input.CleatCount,
                CameraRequested = input.CameraRequested,
                HomeTeamId = home?.TeamId,
                AwayTeamId = away?.TeamId,
                LookingForOpponent = input.LookingForOpponent,
                MinReliability = input.MinReliability is > 0 and <= 100 ? input.MinReliability : null,
                CreatedByPlayerId = creatorId,
                CreatedAt = DateTime.Now,
            };
            context.Matchs.Add(match);
            context.SaveChanges();

            context.MatchParticipants.Add(new MatchParticipant
            {
                MatchId = match.MatchId,
                PlayerId = creatorId,
                Status = ParticipantStatus.Going,
                Side = ParticipantSide.Home,
                Confirmed = true,
                JoinedAt = DateTime.Now,
            });
            AddActivity(context, ActivityType.MatchCreated, creatorId, matchId: match.MatchId);

            // Takımlar: MatchTeam (eski ekranlar için) + üyeler doğrudan kadroya (ev sahibi Side 1, rakip Side 2)
            if (home != null)
            {
                context.MatchTeam.Add(new MatchTeam { MatchId = match.MatchId, TeamId = home.TeamId });
                AddTeamToSquad(context, match, creatorId, creator, home.TeamId, ParticipantSide.Home);
            }
            if (away != null)
            {
                context.MatchTeam.Add(new MatchTeam { MatchId = match.MatchId, TeamId = away.TeamId });
                AddTeamToSquad(context, match, creatorId, creator, away.TeamId, ParticipantSide.Away);
            }

            // İşletmeye rezervasyon talebi
            if (stadium.OwnerPlayerId.HasValue)
            {
                NotificationWriter.Add(context, NotificationType.ReservationRequest,
                    Msg.GetDefault(MessageKeys.Reservation.NotifRequest, DisplayName(creator), stadium.StadiumName, pitch.Name, start, duration),
                    match.MatchId, stadium.StadiumId, new[] { stadium.OwnerPlayerId.Value });
            }

            context.SaveChanges();
            transaction.Commit();
            return OperationResult<int>.Ok(match.MatchId, Msg.Get(MessageKeys.Reservation.RequestSent));
        }

        // ------------------------------------------------------------------ ClaimOpponent

        public OperationResult<bool> ClaimOpponent(int me, int matchId, int teamId)
        {
            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var match = LockMatch(context, matchId);
            if (match == null) return OperationResult<bool>.NotFound();
            var player = context.Players.AsNoTracking().FirstOrDefault(p => p.PlayerId == me);
            if (player == null || player.Role == PlayerRoles.Owner) return OperationResult<bool>.Forbidden(OwnerCannotJoin);
            if (!match.LookingForOpponent || match.AwayTeamId.HasValue)
                return OperationResult<bool>.BadRequest(ErrorCodes.Reservation.NotLookingForOpponent);
            if (match.IsActive == MatchStatus.Rejected)
                return OperationResult<bool>.BadRequest(ErrorCodes.Reservation.MatchRejectedClosed);
            if (IsPast(match))
                return OperationResult<bool>.BadRequest(ErrorCodes.Reservation.MatchPastClosed);

            var team = context.Teams.AsNoTracking().FirstOrDefault(t => t.TeamId == teamId);
            if (team == null) return OperationResult<bool>.NotFound(ErrorCodes.Team.NotFound);
            if (match.HomeTeamId == teamId)
                return OperationResult<bool>.BadRequest(ErrorCodes.Reservation.HomeTeamCannotBeOpponent);
            if (!context.PlayerTeam.Any(pt => pt.TeamId == teamId && pt.PlayerId == me))
                return OperationResult<bool>.Forbidden(ErrorCodes.Reservation.NotTeamMember);

            match.AwayTeamId = teamId;
            match.LookingForOpponent = false;
            context.MatchTeam.Add(new MatchTeam { MatchId = matchId, TeamId = teamId });

            // Ben Going (Side 2)
            var mine = context.MatchParticipants.FirstOrDefault(p => p.MatchId == matchId && p.PlayerId == me);
            if (mine?.Status == ParticipantStatus.Going)
            {
                // Zaten kadrodaysam Deplasman'a geçerim; Deplasman doluysa (Going + misafir) tarafım korunur
                if (mine.Side != ParticipantSide.Away)
                {
                    var awayGoing = context.MatchParticipants
                        .Where(p => p.MatchId == matchId && p.Status == ParticipantStatus.Going && p.PlayerId != me).ToList();
                    if (SideCount(awayGoing, LoadGuests(context, matchId), ParticipantSide.Away) < SideCapacity(match))
                        mine.Side = ParticipantSide.Away;
                }
            }
            else
            {
                if (mine?.Status == ParticipantStatus.Waitlist)
                {
                    // Yedekteysem önce kaydı bırakıp yeniden katılırım (kapasite varsa asile geçerim)
                    mine.Status = ParticipantStatus.Declined;
                    context.SaveChanges();
                }
                var join = Join(context, match, me, ParticipantSide.Away);
                if (!join.Success)
                {
                    return OperationResult<bool>.Fail(
                        join.Status == OperationStatus.Forbidden ? OperationStatus.Forbidden : OperationStatus.BadRequest,
                        join.Code ?? ErrorCodes.Common.OperationFailed, join.Args);
                }
                // Join tercih edilen tarafa (Deplasman) yerleştirir; Deplasman doluysa (Going + misafir)
                // daha az kişili tarafa koyar: taraf kapasitesi aşılmasın diye burada zorlanmaz.
            }

            // Takımın diğer üyeleri doğrudan kadroya (Side 2); kadroda olanlar atlanır
            AddTeamToSquad(context, match, me, player, teamId, ParticipantSide.Away);
            if (match.CreatedByPlayerId.HasValue && match.CreatedByPlayerId.Value != me)
            {
                AddNotification(context, NotificationType.MatchJoin,
                    Msg.GetDefault(MessageKeys.Reservation.NotifOpponentJoined, team.TeamName, match.MatchName), matchId, new[] { match.CreatedByPlayerId.Value });
            }

            context.SaveChanges();
            transaction.Commit();
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Reservation.OpponentAdded));
        }

        // ------------------------------------------------------------------ Rezervasyon durumu

        public OperationResult<bool> SetMatchStatus(int matchId, byte status, string? reason, int? ownerId)
        {
            if (status != MatchStatus.Pending && status != MatchStatus.Approved && status != MatchStatus.Rejected)
                return OperationResult<bool>.BadRequest(ErrorCodes.Reservation.InvalidStatus);

            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var match = LockMatch(context, matchId);
            if (match == null) return OperationResult<bool>.NotFound();
            var stadium = context.Stadiums.AsNoTracking().FirstOrDefault(s => s.StadiumId == match.StadiumId);

            if (ownerId.HasValue)
            {
                if (stadium == null || stadium.OwnerPlayerId != ownerId.Value)
                    return OperationResult<bool>.Forbidden(VenueRules.NotYourVenue);
                var owner = context.Players.AsNoTracking().FirstOrDefault(p => p.PlayerId == ownerId.Value);
                if (!VenueRules.MembershipActive(owner, DateTime.Now))
                    return OperationResult<bool>.Forbidden(VenueRules.MembershipInactive);
                if (match.IsActive != MatchStatus.Pending)
                    return OperationResult<bool>.BadRequest(ErrorCodes.Reservation.NotPending);
                if (status == MatchStatus.Pending)
                    return OperationResult<bool>.BadRequest(ErrorCodes.Reservation.InvalidStatus);
            }
            else if (match.IsActive == MatchStatus.Rejected && status != MatchStatus.Rejected && match.PitchId.HasValue && match.MatchDate.HasValue)
            {
                // Admin geçersiz kılma: reddedilmiş maç geri alınırken saat başkasınca dolmuşsa izin verilmez
                var start = match.MatchDate.Value;
                var end = start.AddMinutes(match.DurationMinutes);
                var ids = new[] { match.PitchId.Value };
                if (VenueRules.BusyMatches(context, ids, start, end, matchId).Count > 0)
                    return OperationResult<bool>.BadRequest(VenueRules.SlotTaken);
            }

            reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            if (reason != null && reason.Length > 200)
                return OperationResult<bool>.BadRequest(ErrorCodes.Validation.TooLong, Msg.Get(MessageKeys.Reservation.LabelRejectReason), 200);

            var previous = match.IsActive;
            match.IsActive = status;
            match.RejectReason = status == MatchStatus.Rejected ? reason : null;

            if (previous != status && status != MatchStatus.Pending)
            {
                var recipients = context.MatchParticipants
                    .Where(p => p.MatchId == matchId && p.Status == ParticipantStatus.Going)
                    .Select(p => p.PlayerId).ToList();
                if (match.CreatedByPlayerId.HasValue) recipients.Add(match.CreatedByPlayerId.Value);
                var place = stadium?.StadiumName ?? Msg.GetDefault(MessageKeys.Reservation.LabelVenueFallback);
                var text = status == MatchStatus.Approved
                    ? Msg.GetDefault(MessageKeys.Reservation.NotifApproved, match.MatchName, match.MatchDate, place)
                    : reason != null
                        ? Msg.GetDefault(MessageKeys.Reservation.NotifRejectedWithReason, match.MatchName, match.MatchDate, place, reason)
                        : Msg.GetDefault(MessageKeys.Reservation.NotifRejected, match.MatchName, match.MatchDate, place);
                AddNotification(context, status == MatchStatus.Approved ? NotificationType.MatchApproval : NotificationType.MatchRejection,
                    text, matchId, recipients);
            }

            context.SaveChanges();
            transaction.Commit();
            return OperationResult<bool>.Ok(true, Msg.Get(status == MatchStatus.Approved ? MessageKeys.Reservation.Approved
                : status == MatchStatus.Rejected ? MessageKeys.Reservation.Rejected : MessageKeys.Reservation.SetPending));
        }
    }
}
