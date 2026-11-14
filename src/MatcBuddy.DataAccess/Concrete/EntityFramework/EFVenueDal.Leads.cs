using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatcBuddy.DataAccess.Concrete.EntityFramework
{
    /// <summary>
    /// Harici sahalardan satış listesi (docs/engagement-api.md §4). Oyuncuların telefonla ayarlayıp eklediği sahalar
    /// ad + şehir bazında tek satırda toplanır (her oyuncu kendi kaydını açtığı için aynı yer birden çok kayıt olabilir).
    /// Admin bu işletmeyle anlaşınca harici kayıtları mevcut bir tesise ya da yeni bir aday tesise bağlar: maçlar
    /// o tesise taşınır, harici kayıtlar listeden düşer (LinkedStadiumId).
    /// </summary>
    public partial class EFVenueDal
    {
        private static string LeadKey(string? name, string? city) => $"{Norm(name)}|{Norm(city)}";

        public List<ExternalVenueLeadDto> ExternalLeads(string? query)
        {
            using var context = new MatcBuddyContext();
            var venues = context.Stadiums.AsNoTracking()
                .Where(s => s.IsExternal && s.LinkedStadiumId == null)
                .ToList();
            if (venues.Count == 0) return new List<ExternalVenueLeadDto>();
            var ids = venues.Select(v => v.StadiumId).ToList();
            var now = DateTime.Now;

            var matches = context.Matchs.AsNoTracking()
                .Where(m => ids.Contains(m.StadiumId) && m.IsActive != MatchStatus.Rejected)
                .Select(m => new { m.MatchId, m.StadiumId, m.MatchDate })
                .ToList();
            var matchIds = matches.Select(m => m.MatchId).ToList();
            var players = context.MatchParticipants.AsNoTracking()
                .Where(p => matchIds.Contains(p.MatchId) && p.Status == ParticipantStatus.Going)
                .Select(p => new { p.MatchId, p.PlayerId })
                .ToList();
            var matchStadium = matches.ToDictionary(m => m.MatchId, m => m.StadiumId);

            var q = Norm(query);
            return venues
                .GroupBy(v => LeadKey(v.StadiumName, v.City))
                .Select(g =>
                {
                    var sIds = g.Select(v => v.StadiumId).ToHashSet();
                    var ms = matches.Where(m => sIds.Contains(m.StadiumId)).ToList();
                    var newest = g.OrderByDescending(v => v.CreatedAt).First();
                    return new ExternalVenueLeadDto
                    {
                        Key = g.Key,
                        Name = newest.StadiumName,
                        City = g.Select(v => v.City).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)),
                        District = g.Select(v => v.District).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d)),
                        Address = g.Select(v => v.Address).FirstOrDefault(a => !string.IsNullOrWhiteSpace(a)),
                        Location = g.Select(v => v.Location).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)),
                        Phones = g.Select(v => v.Phone).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()).Distinct().ToList(),
                        StadiumIds = sIds.OrderBy(i => i).ToList(),
                        AddedBy = g.Select(v => v.CreatedByPlayerId).Distinct().Count(),
                        MatchCount = ms.Count,
                        UpcomingCount = ms.Count(m => m.MatchDate > now),
                        PlayerCount = players.Where(p => matchStadium.TryGetValue(p.MatchId, out var sid) && sIds.Contains(sid))
                            .Select(p => p.PlayerId).Distinct().Count(),
                        FirstMatchDate = ms.Min(m => m.MatchDate),
                        LastMatchDate = ms.Max(m => m.MatchDate),
                    };
                })
                .Where(l => q.Length == 0 || Norm(l.Name).Contains(q) || Norm(l.City).Contains(q) || Norm(l.District).Contains(q))
                .OrderByDescending(l => l.MatchCount).ThenByDescending(l => l.PlayerCount).ThenByDescending(l => l.LastMatchDate)
                .ToList();
        }

        public List<LinkTargetDto> LinkTargets(string? query)
        {
            using var context = new MatcBuddyContext();
            var q = Norm(query);
            return context.Stadiums.AsNoTracking()
                .Where(s => !s.IsExternal)
                .Select(s => new LinkTargetDto { StadiumId = s.StadiumId, Name = s.StadiumName, City = s.City, District = s.District, HasOwner = s.OwnerPlayerId != null })
                .ToList()
                .Where(s => q.Length == 0 || Norm(s.Name).Contains(q) || Norm(s.City).Contains(q) || Norm(s.District).Contains(q))
                .OrderBy(s => s.Name)
                .Take(50)
                .ToList();
        }

        public OperationResult<int> LinkExternal(LinkExternalInput input)
        {
            using var context = new MatcBuddyContext();
            using var transaction = context.Database.BeginTransaction();
            var ids = input.StadiumIds.Distinct().ToList();
            var externals = context.Stadiums.Where(s => ids.Contains(s.StadiumId) && s.IsExternal).ToList();
            if (externals.Count == 0) return OperationResult<int>.BadRequest(ErrorCodes.External.LinkNoVenues);

            Stadium target;
            if (input.CreateCandidate)
            {
                // Aday tesis: işletmesi olmadığı için oyunculara görünmez; admin işletme hesabı verince yayına girer
                var first = externals.OrderByDescending(e => e.CreatedAt).First();
                target = new Stadium
                {
                    StadiumName = input.Name ?? first.StadiumName,
                    City = input.City ?? externals.Select(e => e.City).FirstOrDefault(c => c != null),
                    District = input.District ?? externals.Select(e => e.District).FirstOrDefault(d => d != null),
                    Address = input.Address ?? externals.Select(e => e.Address).FirstOrDefault(a => a != null) ?? string.Empty,
                    Location = input.Location ?? externals.Select(e => e.Location).FirstOrDefault(l => l != null),
                    Phone = input.Phone ?? externals.Select(e => e.Phone).FirstOrDefault(p => p != null),
                    IsListed = true,
                    OpeningTime = new TimeOnly(9, 0),
                    ClosingTime = new TimeOnly(0, 0),
                    CreatedAt = DateTime.Now,
                };
                context.Stadiums.Add(target);
                context.SaveChanges();
            }
            else
            {
                var t = input.TargetStadiumId is > 0 ? context.Stadiums.FirstOrDefault(s => s.StadiumId == input.TargetStadiumId && !s.IsExternal) : null;
                if (t == null) return OperationResult<int>.BadRequest(ErrorCodes.External.LinkTargetInvalid);
                target = t;
            }

            var moved = context.Matchs.Where(m => ids.Contains(m.StadiumId))
                .ExecuteUpdate(u => u.SetProperty(m => m.StadiumId, target.StadiumId));
            foreach (var e in externals) e.LinkedStadiumId = target.StadiumId;
            context.SaveChanges();
            transaction.Commit();
            return OperationResult<int>.Ok(target.StadiumId, Msg.Get(MessageKeys.External.Linked, externals.Count, moved));
        }
    }
}
