using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatcBuddy.DataAccess.Concrete.EntityFramework
{
    /// <summary>
    /// İşletme büyüme araçları (docs/venue-growth-api.md):
    ///  - Boş saat fırsatı: tek bir boş saate indirim (kampanya) + o tesiste / yakında oynamış oyunculara bildirim.
    ///  - Rezervasyon onayında kurucunun güvenilirliği.
    ///  - Telefon müşterileri: önceki müşteriler (hızlı giriş) ve haftalık tekrar eden harici rezervasyon.
    /// </summary>
    public partial class EFVenueDal
    {
        // ------------------------------------------------------------------ Boş saat fırsatı

        /// <summary>Fırsat bildiriminin gideceği oyuncular: önce bu tesiste, sonra aynı ilçedeki tesislerde oynamış olanlar.</summary>
        private static List<int> DealAudience(MatcBuddyContext context, Stadium stadium)
        {
            var since = DateTime.Now.AddDays(-P.Int(ParameterKeys.Venue.DealAudienceDays));
            var max = P.Int(ParameterKeys.Venue.DealMaxRecipients);
            var nearby = context.Stadiums.AsNoTracking()
                .Where(s => s.StadiumId != stadium.StadiumId && !s.IsExternal && s.City == stadium.City
                            && (stadium.District == null || s.District == stadium.District))
                .Select(s => s.StadiumId);

            List<int> PlayedAt(IQueryable<int> stadiumIds) =>
                (from p in context.MatchParticipants.AsNoTracking()
                 join m in context.Matchs.AsNoTracking() on p.MatchId equals m.MatchId
                 join pl in context.Players.AsNoTracking() on p.PlayerId equals pl.PlayerId
                 where stadiumIds.Contains(m.StadiumId) && p.Status == ParticipantStatus.Going
                       && m.MatchDate >= since && !pl.IsDeleted && pl.Role != PlayerRoles.Owner
                 group p by p.PlayerId into g
                 orderby g.Count() descending
                 select g.Key).Take(max).ToList();

            var own = PlayedAt(context.Stadiums.Where(s => s.StadiumId == stadium.StadiumId).Select(s => s.StadiumId));
            if (own.Count >= max || string.IsNullOrEmpty(stadium.City)) return own;
            var rest = PlayedAt(nearby).Where(id => !own.Contains(id)).Take(max - own.Count);
            return own.Concat(rest).ToList();
        }

        private static int DealsSentToday(MatcBuddyContext context, int stadiumId)
        {
            var today = DateTime.Today;
            return context.Campaigns.AsNoTracking().Count(c => c.StadiumId == stadiumId && c.IsDeal && c.DealSentAt >= today);
        }

        public OperationResult<SlotDealAudienceDto> SlotDealAudience(int me, int stadiumId)
        {
            using var context = new MatcBuddyContext();
            var stadium = StadiumOf(context, stadiumId);
            var g = Guard(context, me, stadium, false);
            if (g != null) return Fail<SlotDealAudienceDto>(g);
            var audience = DealAudience(context, stadium!);
            // Tercihi kapalı olanlar sayılmaz
            var count = NotificationWriter.FilterRecipients(context, NotificationType.SlotDeal, audience).Count;
            return OperationResult<SlotDealAudienceDto>.Ok(new SlotDealAudienceDto
            {
                Count = count,
                SentToday = DealsSentToday(context, stadiumId),
                DailyLimit = P.Int(ParameterKeys.Venue.DealMaxPerDay),
            });
        }

        /// <summary>
        /// Boş saate indirim ilanı: o saat (saha, gün, saat aralığı) için tek günlük kampanya oluşturur — oyuncu o saati
        /// uygulamadan kurarken indirimli fiyatı görür — ve kitleye "boş saat" bildirimi gönderir. Saat şu an rezerve
        /// edilebilir olmalı (açık, boş, ön bildirim süresine uygun).
        /// </summary>
        public OperationResult<SlotDealResultDto> CreateSlotDeal(int me, SlotDealInput input)
        {
            using var context = new MatcBuddyContext();
            using var transaction = context.Database.BeginTransaction();
            var pitch = VenueRules.LockPitch(context, input.PitchId);
            if (pitch == null) return OperationResult<SlotDealResultDto>.NotFound(VenueRules.PitchNotFound);
            var stadium = StadiumOf(context, pitch.StadiumId);
            var g = Guard(context, me, stadium, true);
            if (g != null) return Fail<SlotDealResultDto>(g);

            var start = input.Start!.Value;
            var end = start.AddMinutes(input.DurationMinutes);
            if (start <= DateTime.Now) return OperationResult<SlotDealResultDto>.BadRequest(ErrorCodes.Owner.DealNotFuture);
            var maxDays = P.Int(ParameterKeys.Venue.DealMaxDaysAhead);
            if (start > DateTime.Now.AddDays(maxDays)) return OperationResult<SlotDealResultDto>.BadRequest(ErrorCodes.Owner.DealTooFar, maxDays);
            if (end.Date != start.Date && end != start.Date.AddDays(1))
                return OperationResult<SlotDealResultDto>.BadRequest(ErrorCodes.Owner.DealCrossesMidnight);
            var limit = P.Int(ParameterKeys.Venue.DealMaxPerDay);
            if (DealsSentToday(context, stadium!.StadiumId) >= limit)
                return OperationResult<SlotDealResultDto>.BadRequest(ErrorCodes.Owner.DealLimit, limit);

            var pricing = VenueRules.LoadPricing(context, stadium);
            var reason = VenueRules.CheckSlot(context, pricing, pitch, start, input.DurationMinutes);
            if (reason != null) return OperationResult<SlotDealResultDto>.BadRequest(reason);

            var toHour = end.Date > start.Date ? 24 : end.Minute > 0 ? end.Hour + 1 : end.Hour;
            var campaign = new Campaign
            {
                StadiumId = stadium.StadiumId,
                PitchId = pitch.PitchId,
                Title = Msg.GetDefault(MessageKeys.Growth.DealTitle),
                StartDate = start.Date,
                EndDate = start.Date,
                DaysMask = 1 << VenuePricing.DayIndex(start),
                FromHour = start.Hour,
                ToHour = toHour,
                DiscountPercent = input.DiscountPercent,
                IsActive = true,
                IsDeal = true,
                DealSentAt = DateTime.Now,
            };
            context.Campaigns.Add(campaign);
            context.SaveChanges();

            var audience = DealAudience(context, stadium);
            var notification = NotificationWriter.Add(context, NotificationType.SlotDeal,
                Msg.GetDefault(MessageKeys.Growth.NotifDeal, stadium.StadiumName, start, pitch.Name, input.DiscountPercent),
                null, stadium.StadiumId, audience);
            campaign.DealNotifiedCount = notification?.PlayerNotifications.Count ?? 0;
            context.SaveChanges();
            transaction.Commit();
            return OperationResult<SlotDealResultDto>.Ok(
                new SlotDealResultDto { CampaignId = campaign.CampaignId, Notified = campaign.DealNotifiedCount },
                Msg.Get(MessageKeys.Growth.DealSent, campaign.DealNotifiedCount));
        }

        // ------------------------------------------------------------------ Telefon müşterileri

        /// <summary>Önceki harici rezervasyon müşterileri (ad + telefon), en son gelen önce. q: ad ya da telefon içinde arama.</summary>
        public OperationResult<List<OwnerCustomerDto>> Customers(int me, int stadiumId, string? q)
        {
            using var context = new MatcBuddyContext();
            var g = Guard(context, me, StadiumOf(context, stadiumId), false);
            if (g != null) return Fail<List<OwnerCustomerDto>>(g);
            var pitchIds = context.Pitches.AsNoTracking().Where(p => p.StadiumId == stadiumId).Select(p => p.PitchId).ToList();
            var query = context.SlotBlocks.AsNoTracking()
                .Where(b => pitchIds.Contains(b.PitchId) && b.Kind == SlotBlockKind.Manual && b.CustomerName != null);
            q = q?.Trim();
            if (!string.IsNullOrEmpty(q)) query = query.Where(b => b.CustomerName!.Contains(q) || (b.CustomerPhone != null && b.CustomerPhone.Contains(q)));
            var rows = query.OrderByDescending(b => b.StartAt).Take(500)
                .Select(b => new { b.CustomerName, b.CustomerPhone, b.StartAt, b.Price, b.Reason }).ToList();
            var list = rows
                .GroupBy(r => (Name: r.CustomerName!.Trim().ToLowerInvariant(), Phone: (r.CustomerPhone ?? string.Empty).Replace(" ", "")))
                .Select(gr =>
                {
                    var last = gr.First();
                    return new OwnerCustomerDto
                    {
                        Name = last.CustomerName!.Trim(),
                        Phone = last.CustomerPhone,
                        Bookings = gr.Count(),
                        LastAt = last.StartAt,
                        LastPrice = last.Price,
                        LastTitle = string.IsNullOrEmpty(last.Reason) ? null : last.Reason,
                    };
                })
                .OrderByDescending(c => c.LastAt)
                .Take(10)
                .ToList();
            return OperationResult<List<OwnerCustomerDto>>.Ok(list);
        }

        // ------------------------------------------------------------------ Kurucu güvenilirliği

        /// <summary>Rezervasyon listesi için kurucuların güvenilirlik özeti (tek seferde).</summary>
        private static Dictionary<int, OrganizerReliabilityDto> OrganizerReliability(MatcBuddyContext context, List<int> playerIds)
        {
            if (playerIds.Count == 0) return new();
            var badges = ReliabilityCore.Badges(context, playerIds);
            var since = DateTime.Now.AddDays(-180);
            var events = context.ReliabilityEvents.AsNoTracking()
                .Where(e => playerIds.Contains(e.PlayerId))
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new { e.PlayerId, e.Type, e.ReversedAt, e.CreatedAt })
                .Take(playerIds.Count * 60)
                .ToList();
            var now = DateTime.Now;
            var past = context.Matchs.AsNoTracking()
                .Where(m => m.CreatedByPlayerId != null && playerIds.Contains(m.CreatedByPlayerId.Value)
                            && m.IsActive == MatchStatus.Approved && m.MatchDate < now && m.MatchDate >= since)
                .GroupBy(m => m.CreatedByPlayerId!.Value)
                .Select(gr => new { gr.Key, Count = gr.Count() })
                .ToDictionary(x => x.Key, x => x.Count);

            return playerIds.ToDictionary(id => id, id =>
            {
                var mine = events.Where(e => e.PlayerId == id).ToList();
                var recent = mine.Where(e => e.Type != ReliabilityEventType.OrganizerLateCancel).Take(10).ToList();
                var b = badges[id];
                return new OrganizerReliabilityDto
                {
                    Score = b.Score,
                    Level = b.Level,
                    CompletedMatches = b.CompletedMatches,
                    LateCancels = recent.Count(e => e.ReversedAt == null
                        && (e.Type == ReliabilityEventType.LateCancel || e.Type == ReliabilityEventType.VeryLateCancel)),
                    NoShows = recent.Count(e => e.ReversedAt == null && e.Type == ReliabilityEventType.NoShow),
                    OrganizerLateCancels = mine.Count(e => e.Type == ReliabilityEventType.OrganizerLateCancel
                                                           && e.ReversedAt == null && e.CreatedAt >= since),
                    PastReservations = past.TryGetValue(id, out var c) ? c : 0,
                };
            });
        }
    }
}
