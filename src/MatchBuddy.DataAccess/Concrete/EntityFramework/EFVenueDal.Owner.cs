using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatchBuddy.DataAccess.Concrete.EntityFramework
{
    /// <summary>İşletme paneli (/api/Owner). Her işlem tesis sahipliği (403) ve yazmalarda üyelik (403) kontrolünden geçer.</summary>
    public partial class EFVenueDal
    {
        public const int MaxPhotosPerVenue = 20;

        // Sahiplik (+ yazma ise üyelik) kontrolü. null = geçti, aksi halde hata sonucu (mesaj + durum).
        private static (OperationStatus Status, string Message)? Guard(MatchBuddyContext context, int me, Stadium? stadium, bool write)
        {
            if (stadium == null) return (OperationStatus.NotFound, VenueRules.VenueNotFound);
            if (stadium.OwnerPlayerId != me) return (OperationStatus.Forbidden, VenueRules.NotYourVenue);
            if (write)
            {
                var owner = context.Players.AsNoTracking().FirstOrDefault(p => p.PlayerId == me);
                if (!VenueRules.MembershipActive(owner, DateTime.Now)) return (OperationStatus.Forbidden, VenueRules.MembershipInactive);
            }
            return null;
        }

        private static OperationResult<T> Fail<T>((OperationStatus Status, string Message) g) =>
            new(default!, false, g.Message, g.Status);

        private static Stadium? StadiumOf(MatchBuddyContext context, int stadiumId, bool track = false) =>
            track ? context.Stadiums.FirstOrDefault(s => s.StadiumId == stadiumId)
                  : context.Stadiums.AsNoTracking().FirstOrDefault(s => s.StadiumId == stadiumId);

        private static Stadium? StadiumOfPitch(MatchBuddyContext context, int pitchId) =>
            (from p in context.Pitches.AsNoTracking()
             join s in context.Stadiums.AsNoTracking() on p.StadiumId equals s.StadiumId
             where p.PitchId == pitchId
             select s).FirstOrDefault();

        private static List<int> MyStadiumIds(MatchBuddyContext context, int me) =>
            context.Stadiums.AsNoTracking().Where(s => s.OwnerPlayerId == me).Select(s => s.StadiumId).ToList();

        // ------------------------------------------------------------------ MyVenues

        public OwnerVenuesDto MyVenues(int me)
        {
            using var context = new MatchBuddyContext();
            var owner = context.Players.AsNoTracking().FirstOrDefault(p => p.PlayerId == me);
            var now = DateTime.Now;
            var stadiums = context.Stadiums.AsNoTracking().Where(s => s.OwnerPlayerId == me).OrderBy(s => s.StadiumId).ToList();
            var ids = stadiums.Select(s => s.StadiumId).ToList();
            var covers = VenueRules.CoverUrls(context, ids);
            var pitchCounts = context.Pitches.AsNoTracking().Where(p => ids.Contains(p.StadiumId) && p.IsActive)
                .GroupBy(p => p.StadiumId).Select(g => new { g.Key, Count = g.Count() }).ToList().ToDictionary(x => x.Key, x => x.Count);
            var visible = VenueRules.VisibleStadiumIds(context, ids);

            return new OwnerVenuesDto
            {
                MembershipUntil = owner?.MembershipUntil,
                MembershipSuspended = owner?.MembershipSuspended ?? false,
                MembershipActive = VenueRules.MembershipActive(owner, now),
                Venues = stadiums.Select(s => new OwnerVenueDto
                {
                    StadiumId = s.StadiumId,
                    Name = s.StadiumName,
                    CoverPhotoUrl = covers.TryGetValue(s.StadiumId, out var c) ? c : null,
                    PitchCount = pitchCounts.TryGetValue(s.StadiumId, out var n) ? n : 0,
                    IsVisible = visible.Contains(s.StadiumId),
                    Missing = VenueRules.Missing(s, owner, covers.ContainsKey(s.StadiumId), pitchCounts.ContainsKey(s.StadiumId), now),
                }).ToList(),
            };
        }

        // ------------------------------------------------------------------ Rezervasyonlar

        private static List<ReservationDto> BuildReservations(MatchBuddyContext context, List<Match> matches)
        {
            if (matches.Count == 0) return new List<ReservationDto>();
            var matchIds = matches.Select(m => m.MatchId).ToList();
            // Going + misafir
            var going = ParticipationCore.RosterCounts(context, matchIds);
            var creatorIds = matches.Where(m => m.CreatedByPlayerId.HasValue).Select(m => m.CreatedByPlayerId!.Value).Distinct().ToList();
            var creators = context.Players.AsNoTracking().Where(p => creatorIds.Contains(p.PlayerId)).ToList().ToDictionary(p => p.PlayerId);
            var lookup = VenueRules.MatchVenueLookup.Load(context, matches);

            return matches.Select(m =>
            {
                var duration = m.DurationMinutes <= 0 ? 60 : m.DurationMinutes;
                Pitch? pitch = m.PitchId.HasValue && lookup.Pitches.TryGetValue(m.PitchId.Value, out var p) ? p : null;
                ReservationCreatorDto? creator = null;
                if (m.CreatedByPlayerId.HasValue && creators.TryGetValue(m.CreatedByPlayerId.Value, out var c))
                {
                    var s = PlayerSummaryDto.FromPlayer(c);
                    creator = new ReservationCreatorDto
                    {
                        PlayerId = s.PlayerId, PlayerName = s.PlayerName, PlayerSurname = s.PlayerSurname, UserName = s.UserName,
                        Size = s.Size, Weight = s.Weight, Age = s.Age, IsAdmin = s.IsAdmin, Role = s.Role,
                        UserScore = s.UserScore, ProfilePictureUrl = s.ProfilePictureUrl,
                        PhoneNumber = c.IsDeleted ? null : c.PhoneNumber,
                    };
                }
                return new ReservationDto
                {
                    MatchId = m.MatchId,
                    StadiumId = m.StadiumId,
                    MatchName = m.MatchName,
                    Start = m.MatchDate,
                    End = m.MatchDate?.AddMinutes(duration),
                    DurationMinutes = duration,
                    Pitch = pitch == null ? null : new PitchRefDto { PitchId = pitch.PitchId, Name = pitch.Name },
                    Status = m.IsActive,
                    Creator = creator,
                    GoingCount = going.TryGetValue(m.MatchId, out var g) ? g : 0,
                    Capacity = m.UserCount,
                    HomeTeam = m.HomeTeamId.HasValue && lookup.Teams.TryGetValue(m.HomeTeamId.Value, out var ht) ? new TeamRefDto { TeamId = m.HomeTeamId.Value, TeamName = ht } : null,
                    AwayTeam = m.AwayTeamId.HasValue && lookup.Teams.TryGetValue(m.AwayTeamId.Value, out var at) ? new TeamRefDto { TeamId = m.AwayTeamId.Value, TeamName = at } : null,
                    LookingForOpponent = m.LookingForOpponent,
                    CleatCount = m.CleatCount,
                    CameraRequested = m.CameraRequested,
                    PitchPrice = m.PitchPrice,
                    ExtrasPrice = m.ExtrasPrice,
                    TotalPrice = m.TotalPrice,
                    CreatedAt = m.CreatedAt,
                    RejectReason = m.RejectReason,
                };
            }).ToList();
        }

        // stadiumId verilmişse sahiplik kontrolü; yoksa işletmenin tüm tesisleri
        private static (List<int>? Ids, (OperationStatus, string)? Error) ScopeIds(MatchBuddyContext context, int me, int? stadiumId)
        {
            if (stadiumId is > 0)
            {
                var g = Guard(context, me, StadiumOf(context, stadiumId.Value), false);
                return g == null ? (new List<int> { stadiumId.Value }, null) : (null, g);
            }
            return (MyStadiumIds(context, me), null);
        }

        public OperationResult<List<ReservationDto>> Reservations(int me, int? stadiumId, byte? status, DateTime? from, DateTime? to)
        {
            using var context = new MatchBuddyContext();
            var (ids, error) = ScopeIds(context, me, stadiumId);
            if (error != null) return Fail<List<ReservationDto>>(error.Value);

            var query = context.Matchs.AsNoTracking().Where(m => ids!.Contains(m.StadiumId));
            if (status.HasValue) query = query.Where(m => m.IsActive == status.Value);
            if (from.HasValue) query = query.Where(m => m.MatchDate >= from.Value);
            if (to.HasValue) query = query.Where(m => m.MatchDate < to.Value);
            var matches = query.OrderBy(m => m.MatchDate).ThenBy(m => m.MatchId).ToList();
            return OperationResult<List<ReservationDto>>.Ok(BuildReservations(context, matches));
        }

        public OperationResult<OwnerDashboardDto> Dashboard(int me, int? stadiumId)
        {
            using var context = new MatchBuddyContext();
            var (ids, error) = ScopeIds(context, me, stadiumId);
            if (error != null) return Fail<OwnerDashboardDto>(error.Value);

            var now = DateTime.Now;
            var today = DateTime.Today;
            var weekStart = today.AddDays(-VenuePricing.DayIndex(today));
            var weekEnd = weekStart.AddDays(7);

            var matches = context.Matchs.AsNoTracking()
                .Where(m => ids!.Contains(m.StadiumId) && (m.IsActive == MatchStatus.Pending || m.IsActive == MatchStatus.Approved))
                .Where(m => m.MatchDate >= weekStart.AddDays(-1) || m.MatchDate >= now)
                .ToList();

            var todayCount = matches.Count(m => m.MatchDate >= today && m.MatchDate < today.AddDays(1));
            var weekMatches = matches.Where(m => m.MatchDate >= weekStart && m.MatchDate < weekEnd).ToList();
            var future = matches.Where(m => m.MatchDate >= now).OrderBy(m => m.MatchDate).ThenBy(m => m.MatchId).ToList();
            var pending = future.Where(m => m.IsActive == MatchStatus.Pending).ToList();

            // Doluluk: bu haftanın açık saatleri (aktif saha başına) içinde bekleyen/onaylı maç dakikaları
            double openMinutes = 0, bookedMinutes = 0;
            foreach (var sid in ids!)
            {
                var stadium = StadiumOf(context, sid)!;
                var pricing = VenueRules.LoadPricing(context, stadium);
                var activePitches = context.Pitches.AsNoTracking().Count(p => p.StadiumId == sid && p.IsActive);
                openMinutes += VenuePricing.OpenMinutesBetween(weekStart, weekEnd, pricing.Week) * activePitches;
            }
            foreach (var m in matches.Where(m => m.MatchDate.HasValue))
            {
                var s = m.MatchDate!.Value;
                var e = s.AddMinutes(m.DurationMinutes <= 0 ? 60 : m.DurationMinutes);
                var os = s < weekStart ? weekStart : s;
                var oe = e > weekEnd ? weekEnd : e;
                if (oe > os) bookedMinutes += (oe - os).TotalMinutes;
            }

            var reviews = context.VenueReviews.AsNoTracking().Where(r => ids.Contains(r.StadiumId)).Select(r => (double)r.Rating).ToList();

            return OperationResult<OwnerDashboardDto>.Ok(new OwnerDashboardDto
            {
                TodayCount = todayCount,
                WeekCount = weekMatches.Count,
                PendingCount = pending.Count,
                OccupancyPercent = openMinutes > 0 ? (int)Math.Round(Math.Min(100, bookedMinutes * 100 / openMinutes)) : 0,
                WeekRevenue = weekMatches.Where(m => m.IsActive == MatchStatus.Approved).Sum(m => m.TotalPrice ?? 0m),
                Rating = reviews.Count > 0 ? Math.Round(reviews.Average(), 1) : null,
                ReviewCount = reviews.Count,
                Upcoming = BuildReservations(context, future.Take(5).ToList()),
                Pending = BuildReservations(context, pending),
            });
        }

        public OperationResult<OwnerCalendarDto> Calendar(int me, int stadiumId, DateTime from, DateTime to)
        {
            using var context = new MatchBuddyContext();
            var stadium = StadiumOf(context, stadiumId);
            var g = Guard(context, me, stadium, false);
            if (g != null) return Fail<OwnerCalendarDto>(g.Value);

            var pricing = VenueRules.LoadPricing(context, stadium!);
            var pitches = context.Pitches.AsNoTracking().Where(p => p.StadiumId == stadiumId)
                .OrderBy(p => p.Name).ThenBy(p => p.PitchId).ToList();
            var pitchIds = pitches.Select(p => p.PitchId).ToList();

            var lower = from.AddHours(-2);
            var matches = context.Matchs.AsNoTracking()
                .Where(m => m.PitchId != null && pitchIds.Contains(m.PitchId.Value) && m.MatchDate >= lower && m.MatchDate < to)
                .ToList()
                .Where(m => m.MatchDate!.Value.AddMinutes(m.DurationMinutes) > from)
                .OrderBy(m => m.MatchDate).ThenBy(m => m.MatchId)
                .ToList();
            var matchIds = matches.Select(m => m.MatchId).ToList();
            // Going + misafir
            var going = ParticipationCore.RosterCounts(context, matchIds);
            var creatorIds = matches.Where(m => m.CreatedByPlayerId.HasValue).Select(m => m.CreatedByPlayerId!.Value).Distinct().ToList();
            var creators = context.Players.AsNoTracking().Where(p => creatorIds.Contains(p.PlayerId)).ToList()
                .ToDictionary(p => p.PlayerId, PlayerSummaryDto.FromPlayer);

            var items = new List<object>();
            items.AddRange(matches.Select(m => new CalendarMatchItemDto
            {
                MatchId = m.MatchId,
                PitchId = m.PitchId!.Value,
                Start = m.MatchDate!.Value,
                End = m.MatchDate!.Value.AddMinutes(m.DurationMinutes),
                Status = m.IsActive,
                Title = m.MatchName,
                Creator = m.CreatedByPlayerId.HasValue && creators.TryGetValue(m.CreatedByPlayerId.Value, out var c) ? c : null,
                GoingCount = going.TryGetValue(m.MatchId, out var n) ? n : 0,
                Capacity = m.UserCount,
                TotalPrice = m.TotalPrice,
            }));
            items.AddRange(VenueRules.Blocks(context, pitchIds, from, to).OrderBy(b => b.StartAt).Select(b => new CalendarBlockItemDto
            {
                BlockKind = b.Kind,
                SlotBlockId = b.SlotBlockId,
                PitchId = b.PitchId,
                Start = b.StartAt,
                End = b.EndAt,
                Reason = string.IsNullOrEmpty(b.Reason) ? null : b.Reason,
                CustomerName = b.CustomerName,
                CustomerPhone = b.CustomerPhone,
                Price = b.Price,
                IsPaid = b.IsPaid,
                Note = b.Note,
            }));

            return OperationResult<OwnerCalendarDto>.Ok(new OwnerCalendarDto
            {
                Pitches = pitches.Select(p => new PitchRefDto { PitchId = p.PitchId, Name = p.Name }).ToList(),
                OpeningHours = VenueRules.ToDtos(pricing.Week),
                Items = items,
            });
        }

        // ------------------------------------------------------------------ Tesis bilgileri

        public OperationResult<bool> UpdateVenue(int me, VenueUpdateInput input)
        {
            using var context = new MatchBuddyContext();
            var stadium = StadiumOf(context, input.StadiumId, track: true);
            var g = Guard(context, me, stadium, true);
            if (g != null) return Fail<bool>(g.Value);

            stadium!.StadiumName = input.Name!;
            stadium.Description = input.Description;
            stadium.Phone = input.Phone;
            stadium.Address = input.Address;
            stadium.City = input.City;
            stadium.District = input.District;
            stadium.Location = input.Location;
            stadium.CancellationPolicy = input.CancellationPolicy;
            stadium.CleatRental = input.CleatRental;
            stadium.CleatRentalPrice = input.CleatRentalPrice;
            stadium.HasCamera = input.HasCamera;
            stadium.CameraPrice = input.CameraPrice;
            stadium.HasShower = input.HasShower;
            stadium.HasLockerRoom = input.HasLockerRoom;
            stadium.HasParking = input.HasParking;
            stadium.HasCafe = input.HasCafe;
            stadium.HasShuttle = input.HasShuttle;
            stadium.ProvidesBibs = input.ProvidesBibs;
            stadium.ProvidesBall = input.ProvidesBall;
            context.SaveChanges();
            return OperationResult<bool>.Ok(true, "Tesis bilgileri güncellendi.");
        }

        // ------------------------------------------------------------------ Fotoğraflar

        public OperationResult<List<VenuePhotoDto>> Photos(int me, int stadiumId)
        {
            using var context = new MatchBuddyContext();
            var g = Guard(context, me, StadiumOf(context, stadiumId), false);
            if (g != null) return Fail<List<VenuePhotoDto>>(g.Value);
            return OperationResult<List<VenuePhotoDto>>.Ok(context.StadiumPhotos.AsNoTracking()
                .Where(p => p.StadiumId == stadiumId)
                .OrderByDescending(p => p.IsCover).ThenBy(p => p.SortOrder).ThenBy(p => p.PhotoId)
                .Select(p => new VenuePhotoDto { PhotoId = p.PhotoId, Url = p.Url, IsCover = p.IsCover, SortOrder = p.SortOrder })
                .ToList());
        }

        public OperationResult<bool> CheckCanAddPhoto(int me, int stadiumId)
        {
            using var context = new MatchBuddyContext();
            var g = Guard(context, me, StadiumOf(context, stadiumId), true);
            if (g != null) return Fail<bool>(g.Value);
            if (context.StadiumPhotos.Count(p => p.StadiumId == stadiumId) >= MaxPhotosPerVenue)
                return OperationResult<bool>.BadRequest($"Bir tesise en fazla {MaxPhotosPerVenue} fotoğraf eklenebilir.");
            return OperationResult<bool>.Ok(true);
        }

        public OperationResult<VenuePhotoDto> AddPhoto(int me, int stadiumId, string url)
        {
            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();
            var g = Guard(context, me, StadiumOf(context, stadiumId), true);
            if (g != null) return Fail<VenuePhotoDto>(g.Value);

            var existing = context.StadiumPhotos.Where(p => p.StadiumId == stadiumId).ToList();
            if (existing.Count >= MaxPhotosPerVenue)
                return OperationResult<VenuePhotoDto>.BadRequest($"Bir tesise en fazla {MaxPhotosPerVenue} fotoğraf eklenebilir.");
            var photo = new StadiumPhoto
            {
                StadiumId = stadiumId,
                Url = url,
                IsCover = !existing.Any(p => p.IsCover), // ilk foto otomatik kapak
                SortOrder = existing.Count == 0 ? 0 : existing.Max(p => p.SortOrder) + 1,
            };
            context.StadiumPhotos.Add(photo);
            context.SaveChanges();
            transaction.Commit();
            return OperationResult<VenuePhotoDto>.Ok(new VenuePhotoDto { PhotoId = photo.PhotoId, Url = photo.Url, IsCover = photo.IsCover, SortOrder = photo.SortOrder },
                "Fotoğraf yüklendi.");
        }

        public OperationResult<string> DeletePhoto(int me, int photoId)
        {
            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();
            var photo = context.StadiumPhotos.FirstOrDefault(p => p.PhotoId == photoId);
            if (photo == null) return OperationResult<string>.NotFound("Fotoğraf bulunamadı.");
            var g = Guard(context, me, StadiumOf(context, photo.StadiumId), true);
            if (g != null) return Fail<string>(g.Value);

            var others = context.StadiumPhotos.Where(p => p.StadiumId == photo.StadiumId && p.PhotoId != photoId)
                .OrderBy(p => p.SortOrder).ThenBy(p => p.PhotoId).ToList();
            if (others.Count == 0)
                return OperationResult<string>.BadRequest("Tesisin son fotoğrafı silinemez.");
            if (photo.IsCover && !others.Any(p => p.IsCover)) others[0].IsCover = true;
            context.StadiumPhotos.Remove(photo);
            context.SaveChanges();
            transaction.Commit();
            return OperationResult<string>.Ok(photo.Url, "Fotoğraf silindi.");
        }

        public OperationResult<bool> SetCover(int me, int photoId)
        {
            using var context = new MatchBuddyContext();
            var photo = context.StadiumPhotos.AsNoTracking().FirstOrDefault(p => p.PhotoId == photoId);
            if (photo == null) return OperationResult<bool>.NotFound("Fotoğraf bulunamadı.");
            var g = Guard(context, me, StadiumOf(context, photo.StadiumId), true);
            if (g != null) return Fail<bool>(g.Value);

            foreach (var p in context.StadiumPhotos.Where(p => p.StadiumId == photo.StadiumId).ToList())
            {
                p.IsCover = p.PhotoId == photoId;
            }
            context.SaveChanges();
            return OperationResult<bool>.Ok(true, "Kapak fotoğrafı güncellendi.");
        }

        // ------------------------------------------------------------------ Sahalar

        public OperationResult<List<PitchDto>> Pitches(int me, int stadiumId)
        {
            using var context = new MatchBuddyContext();
            var g = Guard(context, me, StadiumOf(context, stadiumId), false);
            if (g != null) return Fail<List<PitchDto>>(g.Value);
            return OperationResult<List<PitchDto>>.Ok(context.Pitches.AsNoTracking().Where(p => p.StadiumId == stadiumId)
                .OrderBy(p => p.Name).ThenBy(p => p.PitchId).ToList().Select(ToDto).ToList());
        }

        public OperationResult<int> SavePitch(int me, PitchSaveInput input)
        {
            using var context = new MatchBuddyContext();
            Pitch? pitch = null;
            var stadiumId = input.StadiumId;
            if (input.PitchId is > 0)
            {
                pitch = context.Pitches.FirstOrDefault(p => p.PitchId == input.PitchId.Value);
                if (pitch == null) return OperationResult<int>.NotFound(VenueRules.PitchNotFound);
                stadiumId = pitch.StadiumId;
            }
            var g = Guard(context, me, StadiumOf(context, stadiumId), true);
            if (g != null) return Fail<int>(g.Value);

            if (pitch == null)
            {
                pitch = new Pitch { StadiumId = stadiumId };
                context.Pitches.Add(pitch);
            }
            pitch.Name = input.Name!;
            pitch.Surface = input.Surface;
            pitch.IsIndoor = input.IsIndoor;
            pitch.PlayersPerSide = input.PlayersPerSide;
            pitch.BasePrice = input.BasePrice;
            pitch.IsActive = input.IsActive;
            context.SaveChanges();
            return OperationResult<int>.Ok(pitch.PitchId, "Saha kaydedildi.");
        }

        public OperationResult<bool> DeletePitch(int me, int pitchId)
        {
            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();
            var pitch = context.Pitches.FirstOrDefault(p => p.PitchId == pitchId);
            if (pitch == null) return OperationResult<bool>.NotFound(VenueRules.PitchNotFound);
            var g = Guard(context, me, StadiumOf(context, pitch.StadiumId), true);
            if (g != null) return Fail<bool>(g.Value);

            var now = DateTime.Now;
            if (context.Matchs.Any(m => m.PitchId == pitchId && m.MatchDate >= now
                                        && (m.IsActive == MatchStatus.Pending || m.IsActive == MatchStatus.Approved)))
                return OperationResult<bool>.BadRequest("Bu sahada gelecekte bekleyen ya da onaylı rezervasyon var. Sahayı silmek yerine pasife alabilirsiniz.");

            // Geçmiş maçlar tesiste kalır, saha bağlantısı kaldırılır; sahaya özel kural/kampanyalar silinir
            context.Matchs.Where(m => m.PitchId == pitchId).ExecuteUpdate(s => s.SetProperty(m => m.PitchId, (int?)null));
            context.PriceRules.Where(r => r.PitchId == pitchId).ExecuteDelete();
            context.Campaigns.Where(c => c.PitchId == pitchId).ExecuteDelete();
            context.Pitches.Remove(pitch);
            context.SaveChanges();
            transaction.Commit();
            return OperationResult<bool>.Ok(true, "Saha silindi.");
        }

        // ------------------------------------------------------------------ Açılış saatleri

        public OperationResult<List<OpeningHourDto>> OpeningHours(int me, int stadiumId)
        {
            using var context = new MatchBuddyContext();
            var stadium = StadiumOf(context, stadiumId);
            var g = Guard(context, me, stadium, false);
            if (g != null) return Fail<List<OpeningHourDto>>(g.Value);
            return OperationResult<List<OpeningHourDto>>.Ok(VenueRules.ToDtos(VenueRules.LoadPricing(context, stadium!).Week));
        }

        public OperationResult<bool> SaveOpeningHours(int me, int stadiumId, List<(int Day, bool IsClosed, TimeSpan Open, TimeSpan Close)> days)
        {
            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();
            var g = Guard(context, me, StadiumOf(context, stadiumId), true);
            if (g != null) return Fail<bool>(g.Value);

            var existing = context.OpeningHours.Where(h => h.StadiumId == stadiumId).ToList().ToDictionary(h => (int)h.DayOfWeek);
            foreach (var d in days)
            {
                if (!existing.TryGetValue(d.Day, out var row))
                {
                    row = new OpeningHour { StadiumId = stadiumId, DayOfWeek = (byte)d.Day };
                    context.OpeningHours.Add(row);
                }
                row.IsClosed = d.IsClosed;
                row.OpenTime = d.Open;
                row.CloseTime = d.Close;
            }
            context.SaveChanges();
            transaction.Commit();
            return OperationResult<bool>.Ok(true, "Açılış saatleri kaydedildi.");
        }

        // ------------------------------------------------------------------ Fiyat kuralları

        public OperationResult<List<PriceRuleDto>> PriceRules(int me, int stadiumId)
        {
            using var context = new MatchBuddyContext();
            var g = Guard(context, me, StadiumOf(context, stadiumId), false);
            if (g != null) return Fail<List<PriceRuleDto>>(g.Value);
            return OperationResult<List<PriceRuleDto>>.Ok(context.PriceRules.AsNoTracking().Where(r => r.StadiumId == stadiumId)
                .OrderBy(r => r.PitchId).ThenBy(r => r.FromHour).ThenBy(r => r.PriceRuleId)
                .Select(r => new PriceRuleDto
                {
                    PriceRuleId = r.PriceRuleId, StadiumId = r.StadiumId, PitchId = r.PitchId, DaysMask = r.DaysMask,
                    FromHour = r.FromHour, ToHour = r.ToHour, PricePerHour = r.PricePerHour,
                }).ToList());
        }

        private static string? CheckPitchBelongs(MatchBuddyContext context, int? pitchId, int stadiumId) =>
            pitchId.HasValue && !context.Pitches.Any(p => p.PitchId == pitchId.Value && p.StadiumId == stadiumId)
                ? "Saha bu tesise ait değil." : null;

        public OperationResult<int> SavePriceRule(int me, PriceRuleDto input)
        {
            using var context = new MatchBuddyContext();
            PriceRule? rule = null;
            var stadiumId = input.StadiumId;
            if (input.PriceRuleId is > 0)
            {
                rule = context.PriceRules.FirstOrDefault(r => r.PriceRuleId == input.PriceRuleId.Value);
                if (rule == null) return OperationResult<int>.NotFound("Fiyat kuralı bulunamadı.");
                stadiumId = rule.StadiumId;
            }
            var g = Guard(context, me, StadiumOf(context, stadiumId), true);
            if (g != null) return Fail<int>(g.Value);
            var pitchError = CheckPitchBelongs(context, input.PitchId, stadiumId);
            if (pitchError != null) return OperationResult<int>.BadRequest(pitchError);

            if (rule == null)
            {
                rule = new PriceRule { StadiumId = stadiumId };
                context.PriceRules.Add(rule);
            }
            rule.PitchId = input.PitchId;
            rule.DaysMask = input.DaysMask;
            rule.FromHour = input.FromHour;
            rule.ToHour = input.ToHour;
            rule.PricePerHour = input.PricePerHour;
            context.SaveChanges();
            return OperationResult<int>.Ok(rule.PriceRuleId, "Fiyat kuralı kaydedildi.");
        }

        public OperationResult<bool> DeletePriceRule(int me, int priceRuleId)
        {
            using var context = new MatchBuddyContext();
            var rule = context.PriceRules.FirstOrDefault(r => r.PriceRuleId == priceRuleId);
            if (rule == null) return OperationResult<bool>.NotFound("Fiyat kuralı bulunamadı.");
            var g = Guard(context, me, StadiumOf(context, rule.StadiumId), true);
            if (g != null) return Fail<bool>(g.Value);
            context.PriceRules.Remove(rule);
            context.SaveChanges();
            return OperationResult<bool>.Ok(true, "Fiyat kuralı silindi.");
        }

        // ------------------------------------------------------------------ Kampanyalar

        public OperationResult<List<CampaignDto>> Campaigns(int me, int stadiumId)
        {
            using var context = new MatchBuddyContext();
            var g = Guard(context, me, StadiumOf(context, stadiumId), false);
            if (g != null) return Fail<List<CampaignDto>>(g.Value);
            return OperationResult<List<CampaignDto>>.Ok(context.Campaigns.AsNoTracking().Where(c => c.StadiumId == stadiumId)
                .OrderByDescending(c => c.EndDate).ThenBy(c => c.CampaignId).ToList().Select(ToDto).ToList());
        }

        public OperationResult<int> SaveCampaign(int me, CampaignSaveInput input)
        {
            using var context = new MatchBuddyContext();
            Campaign? campaign = null;
            var stadiumId = input.StadiumId;
            if (input.CampaignId is > 0)
            {
                campaign = context.Campaigns.FirstOrDefault(c => c.CampaignId == input.CampaignId.Value);
                if (campaign == null) return OperationResult<int>.NotFound("Kampanya bulunamadı.");
                stadiumId = campaign.StadiumId;
            }
            var g = Guard(context, me, StadiumOf(context, stadiumId), true);
            if (g != null) return Fail<int>(g.Value);
            var pitchError = CheckPitchBelongs(context, input.PitchId, stadiumId);
            if (pitchError != null) return OperationResult<int>.BadRequest(pitchError);

            if (campaign == null)
            {
                campaign = new Campaign { StadiumId = stadiumId };
                context.Campaigns.Add(campaign);
            }
            campaign.PitchId = input.PitchId;
            campaign.Title = input.Title!;
            campaign.Description = input.Description;
            campaign.StartDate = input.StartDate!.Value.Date;
            campaign.EndDate = input.EndDate!.Value.Date;
            campaign.DaysMask = input.DaysMask;
            campaign.FromHour = input.FromHour;
            campaign.ToHour = input.ToHour;
            campaign.DiscountPercent = input.DiscountPercent;
            campaign.FixedPrice = input.FixedPrice;
            campaign.IsActive = input.IsActive;
            context.SaveChanges();
            return OperationResult<int>.Ok(campaign.CampaignId, "Kampanya kaydedildi.");
        }

        public OperationResult<bool> DeleteCampaign(int me, int campaignId)
        {
            using var context = new MatchBuddyContext();
            var campaign = context.Campaigns.FirstOrDefault(c => c.CampaignId == campaignId);
            if (campaign == null) return OperationResult<bool>.NotFound("Kampanya bulunamadı.");
            var g = Guard(context, me, StadiumOf(context, campaign.StadiumId), true);
            if (g != null) return Fail<bool>(g.Value);
            context.Campaigns.Remove(campaign);
            context.SaveChanges();
            return OperationResult<bool>.Ok(true, "Kampanya silindi.");
        }

        // ------------------------------------------------------------------ Bloklar

        public OperationResult<List<SlotBlockDto>> Blocks(int me, int stadiumId, DateTime from, DateTime to)
        {
            using var context = new MatchBuddyContext();
            var g = Guard(context, me, StadiumOf(context, stadiumId), false);
            if (g != null) return Fail<List<SlotBlockDto>>(g.Value);
            var pitches = context.Pitches.AsNoTracking().Where(p => p.StadiumId == stadiumId).ToList().ToDictionary(p => p.PitchId);
            return OperationResult<List<SlotBlockDto>>.Ok(VenueRules.Blocks(context, pitches.Keys.ToList(), from, to)
                .OrderBy(b => b.StartAt).ThenBy(b => b.SlotBlockId)
                .Select(BlockMapper(pitches)).ToList());
        }

        private static Func<SlotBlock, SlotBlockDto> BlockMapper(IReadOnlyDictionary<int, Pitch> pitches) => b => new SlotBlockDto
        {
            SlotBlockId = b.SlotBlockId,
            PitchId = b.PitchId,
            PitchName = pitches.TryGetValue(b.PitchId, out var p) ? p.Name : null,
            StartAt = b.StartAt,
            EndAt = b.EndAt,
            Kind = b.Kind,
            Reason = string.IsNullOrEmpty(b.Reason) ? null : b.Reason,
            CustomerName = b.CustomerName,
            CustomerPhone = b.CustomerPhone,
            Price = b.Price,
            IsPaid = b.IsPaid,
            Note = b.Note,
        };

        /// <summary>
        /// Blok (Kind 0) ya da harici rezervasyon (Kind 1) kaydı. Çakışma: aynı sahada bekleyen/onaylı maç → 400;
        /// harici rezervasyon başka bir blokla/rezervasyonla, kapalı blok da bir harici rezervasyonla çakışamaz
        /// (güncellemede kaydın kendisi hariç). Harici rezervasyon geçmiş tarihli de girilebilir (gelir kaydı için).
        /// Girdi doğrulaması (kind, müşteri adı, fiyat, uzunluklar) VenueManager'da.
        /// </summary>
        public OperationResult<int> SaveBlock(int me, BlockSaveInput input)
        {
            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();
            SlotBlock? block = null;
            if (input.SlotBlockId is > 0)
            {
                block = context.SlotBlocks.FirstOrDefault(b => b.SlotBlockId == input.SlotBlockId.Value);
                if (block == null) return OperationResult<int>.NotFound("Blok bulunamadı.");
                var gOld = Guard(context, me, StadiumOfPitch(context, block.PitchId), true);
                if (gOld != null) return Fail<int>(gOld.Value);
            }
            var pitch = VenueRules.LockPitch(context, input.PitchId);
            if (pitch == null) return OperationResult<int>.NotFound(VenueRules.PitchNotFound);
            var g = Guard(context, me, StadiumOf(context, pitch.StadiumId), true);
            if (g != null) return Fail<int>(g.Value);

            var kind = (byte)input.Kind;
            var start = input.StartAt!.Value;
            var end = input.EndAt!.Value;
            var ids = new[] { pitch.PitchId };
            if (VenueRules.BusyMatches(context, ids, start, end).Count > 0)
                return OperationResult<int>.BadRequest("Bu aralıkta bekleyen ya da onaylı bir rezervasyon var.");
            var selfId = block?.SlotBlockId ?? 0;
            var others = VenueRules.Blocks(context, ids, start, end).Where(b => b.SlotBlockId != selfId).ToList();
            if (kind == SlotBlockKind.Manual && others.Count > 0)
                return OperationResult<int>.BadRequest(others.Any(b => b.Kind == SlotBlockKind.Manual)
                    ? "Bu aralıkta başka bir harici rezervasyon var."
                    : "Bu aralık kapalı (bakım/blok) olarak işaretli.");
            if (kind == SlotBlockKind.Closed && others.Any(b => b.Kind == SlotBlockKind.Manual))
                return OperationResult<int>.BadRequest("Bu aralıkta bir harici rezervasyon var.");

            if (block == null)
            {
                block = new SlotBlock();
                context.SlotBlocks.Add(block);
            }
            block.PitchId = pitch.PitchId;
            block.StartAt = start;
            block.EndAt = end;
            block.Kind = kind;
            if (kind == SlotBlockKind.Manual)
            {
                block.Reason = input.Reason ?? string.Empty;
                block.CustomerName = input.CustomerName;
                block.CustomerPhone = input.CustomerPhone;
                block.Price = input.Price;
                block.IsPaid = input.IsPaid ?? false;
                block.Note = input.Note;
            }
            else
            {
                block.Reason = input.Reason ?? "Kapalı";
                block.CustomerName = null;
                block.CustomerPhone = null;
                block.Price = null;
                block.IsPaid = false;
                block.Note = input.Note;
            }
            context.SaveChanges();
            transaction.Commit();
            return OperationResult<int>.Ok(block.SlotBlockId, kind == SlotBlockKind.Manual ? "Rezervasyon kaydedildi." : "Saat kapatıldı.");
        }

        public OperationResult<bool> SetBlockPaid(int me, int slotBlockId, bool isPaid)
        {
            using var context = new MatchBuddyContext();
            var block = context.SlotBlocks.FirstOrDefault(b => b.SlotBlockId == slotBlockId);
            if (block == null) return OperationResult<bool>.NotFound("Blok bulunamadı.");
            var g = Guard(context, me, StadiumOfPitch(context, block.PitchId), true);
            if (g != null) return Fail<bool>(g.Value);
            if (block.Kind != SlotBlockKind.Manual)
                return OperationResult<bool>.BadRequest("Ödeme durumu sadece harici rezervasyonlar için değiştirilebilir.");
            block.IsPaid = isPaid;
            context.SaveChanges();
            return OperationResult<bool>.Ok(true, isPaid ? "Ödeme alındı olarak işaretlendi." : "Ödeme bekleniyor olarak işaretlendi.");
        }

        public OperationResult<bool> DeleteBlock(int me, int slotBlockId)
        {
            using var context = new MatchBuddyContext();
            var block = context.SlotBlocks.FirstOrDefault(b => b.SlotBlockId == slotBlockId);
            if (block == null) return OperationResult<bool>.NotFound("Blok bulunamadı.");
            var g = Guard(context, me, StadiumOfPitch(context, block.PitchId), true);
            if (g != null) return Fail<bool>(g.Value);
            context.SlotBlocks.Remove(block);
            context.SaveChanges();
            return OperationResult<bool>.Ok(true, "Blok kaldırıldı.");
        }

        // ------------------------------------------------------------------ Yorumlar

        public OperationResult<List<VenueReviewDto>> OwnerReviews(int me, int stadiumId)
        {
            using var context = new MatchBuddyContext();
            var g = Guard(context, me, StadiumOf(context, stadiumId), false);
            if (g != null) return Fail<List<VenueReviewDto>>(g.Value);
            var reviews = context.VenueReviews.AsNoTracking().Where(r => r.StadiumId == stadiumId)
                .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.ReviewId).ToList();
            var ids = reviews.Select(r => r.PlayerId).Distinct().ToList();
            var players = context.Players.AsNoTracking().Where(p => ids.Contains(p.PlayerId)).ToList()
                .ToDictionary(p => p.PlayerId, PlayerSummaryDto.FromPlayer);
            var matches = ReviewMatchRefs(context, reviews.Select(r => r.MatchId));
            return OperationResult<List<VenueReviewDto>>.Ok(reviews.Select(r => new VenueReviewDto
            {
                ReviewId = r.ReviewId, MatchId = r.MatchId, Rating = r.Rating, Comment = r.Comment, CreatedAt = r.CreatedAt,
                Player = players.TryGetValue(r.PlayerId, out var p) ? p : null, OwnerReply = r.OwnerReply, RepliedAt = r.RepliedAt,
                Match = matches.TryGetValue(r.MatchId, out var m) ? m : null,
            }).ToList());
        }

        public OperationResult<bool> ReplyReview(int me, int reviewId, string reply)
        {
            using var context = new MatchBuddyContext();
            var review = context.VenueReviews.FirstOrDefault(r => r.ReviewId == reviewId);
            if (review == null) return OperationResult<bool>.NotFound("Yorum bulunamadı.");
            var g = Guard(context, me, StadiumOf(context, review.StadiumId), true);
            if (g != null) return Fail<bool>(g.Value);
            review.OwnerReply = reply;
            review.RepliedAt = DateTime.Now;
            context.SaveChanges();
            return OperationResult<bool>.Ok(true, "Yanıtınız kaydedildi.");
        }
    }
}
