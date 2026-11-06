using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatchBuddy.DataAccess.Concrete.EntityFramework
{
    /// <summary>Admin: işletme hesapları, üyelik, tesis atama, yayın anahtarı, OSM içe aktarma.</summary>
    public partial class EFVenueDal
    {
        public List<AdminOwnerDto> Owners()
        {
            using var context = new MatchBuddyContext();
            var now = DateTime.Now;
            var owners = context.Players.AsNoTracking().Where(p => p.Role == PlayerRoles.Owner && !p.IsDeleted)
                .OrderBy(p => p.PlayerName).ThenBy(p => p.PlayerId).ToList();
            var ownerIds = owners.Select(o => o.PlayerId).ToList();
            var stadiums = context.Stadiums.AsNoTracking()
                .Where(s => s.OwnerPlayerId != null && ownerIds.Contains(s.OwnerPlayerId.Value)).ToList();
            var visible = VenueRules.VisibleStadiumIds(context, stadiums.Select(s => s.StadiumId));

            return owners.Select(o =>
            {
                var mine = stadiums.Where(s => s.OwnerPlayerId == o.PlayerId).OrderBy(s => s.StadiumId).ToList();
                return new AdminOwnerDto
                {
                    Owner = new AdminOwnerInfoDto
                    {
                        PlayerId = o.PlayerId, Name = o.PlayerName, ContactName = o.PlayerSurname, UserName = o.UserName,
                        Email = o.Email, PhoneNumber = o.PhoneNumber,
                    },
                    MembershipUntil = o.MembershipUntil,
                    MembershipSuspended = o.MembershipSuspended,
                    MembershipActive = VenueRules.MembershipActive(o, now),
                    Venues = mine.Select(s => new AdminOwnerVenueDto
                    {
                        StadiumId = s.StadiumId, Name = s.StadiumName, IsVisible = visible.Contains(s.StadiumId), IsListed = s.IsListed,
                    }).ToList(),
                    // Players'ta oluşturulma zamanı yok: işletmenin ilk tesisinin oluşturulma zamanı
                    CreatedAt = mine.Count > 0 ? mine.Min(s => s.CreatedAt) : null,
                };
            }).ToList();
        }

        /// <summary>Admin: işletmenin tüm bilgileri. Şifre hash olarak saklandığı için DÖNMEZ.</summary>
        public OperationResult<AdminOwnerDetailDto> OwnerDetail(int ownerId)
        {
            var summary = Owners().FirstOrDefault(o => o.Owner.PlayerId == ownerId);
            if (summary == null) return OperationResult<AdminOwnerDetailDto>.NotFound("İşletme bulunamadı.");

            using var context = new MatchBuddyContext();
            var owner = context.Players.AsNoTracking().First(p => p.PlayerId == ownerId);
            var stadiums = context.Stadiums.AsNoTracking().Where(s => s.OwnerPlayerId == ownerId).OrderBy(s => s.StadiumId).ToList();
            var now = DateTime.Now;

            var details = stadiums.Select(s =>
            {
                var dto = BuildDetail<OwnerVenueDetailDto>(context, s, ownerView: true);
                dto.IsListed = s.IsListed;
                dto.IsVisible = VenueRules.IsVisible(context, s.StadiumId);
                dto.Missing = VenueRules.Missing(s, owner, dto.Photos.Count > 0, dto.Pitches.Any(p => p.IsActive), now);
                return dto;
            }).ToList();

            var ids = stadiums.Select(s => s.StadiumId).ToList();
            var matches = context.Matchs.AsNoTracking().Where(m => ids.Contains(m.StadiumId) && m.PitchId != null)
                .Select(m => new { m.IsActive, m.MatchDate, m.TotalPrice, m.CreatedAt }).ToList();

            return OperationResult<AdminOwnerDetailDto>.Ok(new AdminOwnerDetailDto
            {
                Owner = summary.Owner,
                MembershipUntil = summary.MembershipUntil,
                MembershipSuspended = summary.MembershipSuspended,
                MembershipActive = summary.MembershipActive,
                Venues = summary.Venues,
                CreatedAt = summary.CreatedAt,
                VenueDetails = details,
                Stats = new AdminOwnerStatsDto
                {
                    TotalReservations = matches.Count(m => m.IsActive != 3),
                    PendingCount = matches.Count(m => m.IsActive == 1 && m.MatchDate > now),
                    UpcomingApprovedCount = matches.Count(m => m.IsActive == 2 && m.MatchDate > now),
                    RejectedCount = matches.Count(m => m.IsActive == 3),
                    ApprovedRevenue = matches.Where(m => m.IsActive == 2 && m.MatchDate <= now).Sum(m => m.TotalPrice ?? 0),
                    LastReservationAt = matches.Count > 0 ? matches.Max(m => m.CreatedAt) : null,
                },
            });
        }

        /// <summary>Admin: işletme hesabına yeni şifre hash'i yazar. Dönüş: kullanıcı adı.</summary>
        public OperationResult<string> SetOwnerPassword(int ownerId, string passwordHash)
        {
            using var context = new MatchBuddyContext();
            var owner = context.Players.FirstOrDefault(p => p.PlayerId == ownerId && p.Role == PlayerRoles.Owner && !p.IsDeleted);
            if (owner == null) return OperationResult<string>.NotFound("İşletme bulunamadı.");
            owner.Password = passwordHash;
            context.SaveChanges();
            return OperationResult<string>.Ok(owner.UserName ?? string.Empty);
        }

        public bool UserNameTaken(string userName)
        {
            using var context = new MatchBuddyContext();
            return context.Players.Any(p => p.UserName == userName);
        }

        public OperationResult<CreateOwnerResultDto> CreateOwner(CreateOwnerInput input, string passwordHash, List<string> photoUrls)
        {
            var o = input.Owner!;
            var v = input.Venue!;
            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            if (context.Players.Any(p => p.UserName == o.UserName))
                return OperationResult<CreateOwnerResultDto>.BadRequest("Bu kullanıcı adı zaten kullanılıyor.");

            var owner = new Player
            {
                PlayerName = o.Name,
                PlayerSurname = o.ContactName ?? string.Empty,
                UserName = o.UserName,
                Password = passwordHash,
                Email = o.Email ?? string.Empty,
                PhoneNumber = o.PhoneNumber ?? string.Empty,
                Address = v.Address ?? string.Empty,
                Role = PlayerRoles.Owner,
                IsAdmin = 0,
                UserScore = 0,
                ProfilePictureUrl = photoUrls.FirstOrDefault(),
                MembershipUntil = o.MembershipUntil,
                MembershipSuspended = false,
            };
            context.Players.Add(owner);
            context.SaveChanges();

            var stadium = new Stadium
            {
                StadiumName = v.Name,
                City = v.City,
                District = v.District,
                Address = v.Address,
                Location = v.Location,
                Phone = v.Phone,
                Description = v.Description,
                OwnerPlayerId = owner.PlayerId,
                IsListed = true,
                OpeningTime = new TimeOnly(9, 0),
                ClosingTime = new TimeOnly(0, 0),
                CreatedAt = DateTime.Now,
            };
            context.Stadiums.Add(stadium);
            context.SaveChanges();

            for (var i = 0; i < photoUrls.Count; i++)
            {
                context.StadiumPhotos.Add(new StadiumPhoto { StadiumId = stadium.StadiumId, Url = photoUrls[i], IsCover = i == 0, SortOrder = i });
            }
            context.Pitches.Add(new Pitch
            {
                StadiumId = stadium.StadiumId, Name = "Saha 1", Surface = 1, IsIndoor = true, PlayersPerSide = 7, BasePrice = 1500m, IsActive = true,
            });
            for (byte d = 0; d < 7; d++)
            {
                context.OpeningHours.Add(new OpeningHour
                {
                    StadiumId = stadium.StadiumId, DayOfWeek = d, IsClosed = false, OpenTime = new TimeSpan(9, 0, 0), CloseTime = TimeSpan.Zero,
                });
            }

            try
            {
                context.SaveChanges();
                transaction.Commit();
            }
            catch (DbUpdateException)
            {
                return OperationResult<CreateOwnerResultDto>.BadRequest("İşletme oluşturulamadı (kullanıcı adı kullanımda olabilir).");
            }
            return OperationResult<CreateOwnerResultDto>.Ok(new CreateOwnerResultDto { OwnerId = owner.PlayerId, StadiumId = stadium.StadiumId },
                "İşletme hesabı ve tesis oluşturuldu.");
        }

        public OperationResult<bool> UpdateMembership(int ownerId, DateTime? membershipUntil, bool suspended)
        {
            using var context = new MatchBuddyContext();
            var owner = context.Players.FirstOrDefault(p => p.PlayerId == ownerId && !p.IsDeleted);
            if (owner == null || owner.Role != PlayerRoles.Owner) return OperationResult<bool>.NotFound("İşletme bulunamadı.");
            owner.MembershipUntil = membershipUntil;
            owner.MembershipSuspended = suspended;
            context.SaveChanges();
            return OperationResult<bool>.Ok(true, "Üyelik güncellendi.");
        }

        public OperationResult<bool> AssignVenue(int stadiumId, int ownerId)
        {
            using var context = new MatchBuddyContext();
            var stadium = context.Stadiums.FirstOrDefault(s => s.StadiumId == stadiumId);
            if (stadium == null) return OperationResult<bool>.NotFound(VenueRules.VenueNotFound);
            var owner = context.Players.AsNoTracking().FirstOrDefault(p => p.PlayerId == ownerId && !p.IsDeleted);
            if (owner == null || owner.Role != PlayerRoles.Owner) return OperationResult<bool>.BadRequest("Seçilen hesap bir işletme değil.");
            stadium.OwnerPlayerId = ownerId;
            context.SaveChanges();
            return OperationResult<bool>.Ok(true, "Tesis işletmeye atandı.");
        }

        public OperationResult<bool> SetListed(int stadiumId, bool isListed)
        {
            using var context = new MatchBuddyContext();
            var stadium = context.Stadiums.FirstOrDefault(s => s.StadiumId == stadiumId);
            if (stadium == null) return OperationResult<bool>.NotFound(VenueRules.VenueNotFound);
            stadium.IsListed = isListed;
            context.SaveChanges();
            return OperationResult<bool>.Ok(true, isListed ? "Tesis yayına alındı." : "Tesis yayından kaldırıldı.");
        }

        public List<VenueGeoRefDto> GeoRefs()
        {
            using var context = new MatchBuddyContext();
            return context.Stadiums.AsNoTracking()
                .Select(s => new VenueGeoRefDto { StadiumId = s.StadiumId, OsmId = s.OsmId, Location = s.Location })
                .ToList();
        }

        public OperationResult<int> ImportCandidate(ImportCandidateInput input)
        {
            using var context = new MatchBuddyContext();
            if (input.OsmId > 0 && context.Stadiums.Any(s => s.OsmId == input.OsmId))
                return OperationResult<int>.BadRequest("Bu OpenStreetMap kaydı zaten içe aktarılmış.");
            var stadium = new Stadium
            {
                StadiumName = input.Name,
                City = input.City,
                District = input.District,
                Address = input.Address ?? string.Empty,
                Location = VenueGeo.Format(input.Lat, input.Lon),
                Phone = input.Phone,
                OsmId = input.OsmId > 0 ? input.OsmId : null,
                IsListed = true, // işletmesi olmadığı için görünmez
                OpeningTime = new TimeOnly(9, 0),
                ClosingTime = new TimeOnly(0, 0),
                CreatedAt = DateTime.Now,
            };
            context.Stadiums.Add(stadium);
            context.SaveChanges();
            return OperationResult<int>.Ok(stadium.StadiumId, "Tesis içe aktarıldı.");
        }
    }
}
