using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using static MatcBuddy.DataAccess.Concrete.EntityFramework.ParticipationCore;

namespace MatcBuddy.DataAccess.Concrete.EntityFramework
{
    /// <summary>
    /// Harici (anlaşmasız) sahada maç (docs/external-venues-api.md). Oyuncu sahayı telefonla kendisi ayarlar:
    /// sistemde işletmesi, sahası ve fiyat kuralı olmayan yer, kurucuya ait bir Stadiums kaydı olarak tutulur
    /// (IsExternal = 1, IsListed = 0). Böylece maç listeleri, takvim, bildirimler ve sohbet değişmeden çalışır.
    /// İşletme onayı olmadığı için maç doğrudan onaylı (Approved) açılır.
    /// </summary>
    public partial class EFVenueDal
    {
        /// <summary>Harici sahayı bulur ya da oluşturur (kurucunun aynı adlı sahası tekrar kullanılır). SaveChanges çağırmaz.</summary>
        private static OperationResult<Stadium>? ResolveExternalVenue(MatcBuddyContext context, int creatorId, ExternalMatchInput input, out Stadium? stadium)
        {
            stadium = null;
            if (input.ExternalVenueId is > 0)
            {
                stadium = context.Stadiums.FirstOrDefault(s => s.StadiumId == input.ExternalVenueId.Value && s.IsExternal && s.CreatedByPlayerId == creatorId);
                if (stadium == null) return OperationResult<Stadium>.NotFound(ErrorCodes.External.VenueNotFound);
            }
            else
            {
                var name = Norm(input.VenueName);
                stadium = context.Stadiums.Where(s => s.IsExternal && s.CreatedByPlayerId == creatorId).ToList()
                    .FirstOrDefault(s => Norm(s.StadiumName) == name);
                if (stadium == null)
                {
                    stadium = new Stadium
                    {
                        StadiumName = input.VenueName!.Trim(),
                        IsExternal = true,
                        IsListed = false,
                        CreatedByPlayerId = creatorId,
                        CreatedAt = DateTime.Now,
                    };
                    context.Stadiums.Add(stadium);
                }
            }

            // Yeni girilen bilgiler sahanın kaydını günceller (boş bırakılanlar korunur)
            if (input.Address != null) stadium.Address = input.Address;
            if (input.City != null) stadium.City = input.City;
            if (input.District != null) stadium.District = input.District;
            if (input.Phone != null) stadium.Phone = input.Phone;
            if (input.Location != null) stadium.Location = input.Location;
            return null;
        }

        public OperationResult<int> CreateExternalMatch(int creatorId, ExternalMatchInput input)
        {
            using var context = new MatcBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var creator = context.Players.AsNoTracking().FirstOrDefault(p => p.PlayerId == creatorId && !p.IsDeleted);
            if (creator == null) return OperationResult<int>.NotFound(ErrorCodes.Player.NotFound);
            if (creator.Role == PlayerRoles.Owner) return OperationResult<int>.Forbidden(ErrorCodes.Reservation.OwnerCannotCreate);

            var min = VenuePricing.MinUserCount();
            var max = VenuePricing.MaxUserCount(P.Int(ParameterKeys.Venue.PlayersPerSideMax));
            if (input.UserCount < min || input.UserCount > max)
                return OperationResult<int>.BadRequest(ErrorCodes.Validation.OutOfRange, Msg.Get(MessageKeys.Reservation.LabelUserCount), min, max);

            var teamError = LoadTeams(context, creatorId, input.HomeTeamId, input.AwayTeamId, input.LookingForOpponent, out var home, out var away);
            if (teamError != null) return teamError;

            var venueError = ResolveExternalVenue(context, creatorId, input, out var stadium);
            if (venueError != null) return OperationResult<int>.NotFound(ErrorCodes.External.VenueNotFound);
            context.SaveChanges();

            var price = input.TotalPrice is > 0 ? VenuePricing.Round2(input.TotalPrice.Value) : (decimal?)null;
            var match = new Match
            {
                MatchName = input.MatchName!.Trim(),
                Description = input.Description?.Trim() ?? string.Empty,
                MatchDate = input.MatchDate!.Value,
                UserCount = input.UserCount,
                // İşletme onayı yok: saha oyuncunun kendi rezervasyonu
                IsActive = MatchStatus.Approved,
                Likes = 0,
                // Admin bu sahayı sistemdeki bir tesise bağladıysa maç o tesiste açılır (docs/engagement-api.md §4)
                StadiumId = stadium!.LinkedStadiumId ?? stadium.StadiumId,
                PitchId = null,
                DurationMinutes = input.DurationMinutes ?? 60,
                PitchPrice = price,
                ExtrasPrice = price.HasValue ? 0m : null,
                TotalPrice = price,
                HomeTeamId = home?.TeamId,
                AwayTeamId = away?.TeamId,
                LookingForOpponent = input.LookingForOpponent,
                MinReliability = input.MinReliability is > 0 and <= 100 ? input.MinReliability : null,
                CreatedByPlayerId = creatorId,
                CreatedAt = DateTime.Now,
            };
            context.Matchs.Add(match);
            context.SaveChanges();

            AddCreatorAndTeams(context, match, creator, home, away);

            context.SaveChanges();
            transaction.Commit();
            return OperationResult<int>.Ok(match.MatchId, Msg.Get(MessageKeys.External.Created));
        }

        public OperationResult<bool> UpdateExternalMatch(int actorId, bool isAdmin, UpdateExternalMatchInput input)
        {
            using var context = new MatcBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var match = LockMatch(context, input.MatchId);
            if (match == null) return OperationResult<bool>.NotFound(ErrorCodes.Match.NotFound);
            var stadium = context.Stadiums.FirstOrDefault(s => s.StadiumId == match.StadiumId);
            if (stadium == null || !stadium.IsExternal) return OperationResult<bool>.BadRequest(ErrorCodes.External.NotExternal);
            if (!isAdmin && match.CreatedByPlayerId != actorId) return OperationResult<bool>.Forbidden(ErrorCodes.Match.OrganizerOnly);
            if (match.IsActive == MatchStatus.Rejected || IsPast(match)) return OperationResult<bool>.BadRequest(ErrorCodes.External.MatchClosed);

            var oldDate = match.MatchDate;
            var oldVenue = stadium.StadiumName;
            if (input.MatchDate.HasValue) match.MatchDate = input.MatchDate.Value;
            if (input.DurationMinutes.HasValue) match.DurationMinutes = input.DurationMinutes.Value;
            if (input.TotalPrice.HasValue)
            {
                var price = input.TotalPrice.Value > 0 ? VenuePricing.Round2(input.TotalPrice.Value) : (decimal?)null;
                match.PitchPrice = price;
                match.ExtrasPrice = price.HasValue ? 0m : null;
                match.TotalPrice = price;
            }
            if (input.VenueName != null) stadium.StadiumName = input.VenueName;
            if (input.Address != null) stadium.Address = input.Address;
            if (input.Phone != null) stadium.Phone = input.Phone;
            if (input.Location != null) stadium.Location = input.Location;

            // Saat ya da yer değiştiyse kadroya haber ver (yeni saate göre hatırlatma tekrar gönderilsin)
            if (oldDate != match.MatchDate || oldVenue != stadium.StadiumName)
            {
                var rows = context.MatchParticipants.Where(p => p.MatchId == match.MatchId && p.Status == ParticipantStatus.Going).ToList();
                foreach (var r in rows) r.ReminderSentAt = null;
                var recipients = rows.Select(r => r.PlayerId).Where(id => id != actorId).ToList();
                if (recipients.Count > 0)
                {
                    AddNotification(context, NotificationType.MatchCreated,
                        Msg.GetDefault(MessageKeys.External.NotifUpdated, match.MatchName, match.MatchDate, stadium.StadiumName),
                        match.MatchId, recipients);
                }
            }

            context.SaveChanges();
            transaction.Commit();
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.External.Updated));
        }

        public List<ExternalVenueDto> MyExternalVenues(int me)
        {
            using var context = new MatcBuddyContext();
            var venues = context.Stadiums.AsNoTracking()
                .Where(s => s.IsExternal && s.CreatedByPlayerId == me)
                .ToList();
            if (venues.Count == 0) return new List<ExternalVenueDto>();
            var ids = venues.Select(v => v.StadiumId).ToList();
            var stats = context.Matchs.AsNoTracking()
                .Where(m => ids.Contains(m.StadiumId))
                .GroupBy(m => m.StadiumId)
                .Select(g => new { StadiumId = g.Key, Count = g.Count(), Last = g.Max(m => m.MatchDate) })
                .ToList()
                .ToDictionary(x => x.StadiumId);
            return venues
                .Select(v =>
                {
                    stats.TryGetValue(v.StadiumId, out var st);
                    return new ExternalVenueDto
                    {
                        StadiumId = v.StadiumId,
                        Name = v.StadiumName,
                        Address = v.Address,
                        City = v.City,
                        District = v.District,
                        Phone = v.Phone,
                        Location = v.Location,
                        MatchCount = st?.Count ?? 0,
                        LastMatchDate = st?.Last,
                    };
                })
                .OrderByDescending(v => v.LastMatchDate ?? DateTime.MinValue)
                .ThenBy(v => v.Name)
                .ToList();
        }
    }
}
