using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using static MatcBuddy.DataAccess.Concrete.EntityFramework.ParticipationCore;

namespace MatcBuddy.DataAccess.Concrete.EntityFramework
{
    /// <summary>
    /// Haftalık sabit maç (docs/venue-growth-api.md). Kurucu maçı "her hafta" diye kurar; seri her zaman en fazla
    /// SeriesLeadDays (7) gün ilerisini açık tutar: sıradaki hafta yaklaşınca aynı saha/saat için yeni maç oluşturulur,
    /// önceki haftanın oyuncularına "bu hafta da geliyor musun?" daveti gider. İşletme serinin bir maçını onayladıktan
    /// sonra yeni haftalar otomatik onaylı açılır. Saat doluysa o hafta atlanır, kurucuya haber verilir.
    /// </summary>
    public static class MatchSeriesService
    {
        /// <summary>Yeni haftalık seri (CreateReservation içinde, maç kaydedildikten sonra). SaveChanges çağırmaz.</summary>
        public static void Start(MatcBuddyContext context, Match first)
        {
            var series = new MatchSeries
            {
                CreatedByPlayerId = first.CreatedByPlayerId!.Value,
                StadiumId = first.StadiumId,
                PitchId = first.PitchId!.Value,
                NextDate = first.MatchDate!.Value.AddDays(7),
                IsActive = true,
                Approved = false,
                CreatedAt = DateTime.Now,
            };
            context.MatchSeries.Add(series);
            context.SaveChanges();
            first.SeriesId = series.SeriesId;
        }

        /// <summary>İşletme serinin bir maçını onayladı / reddetti. Reddedilen ilk maç seriyi durdurur.</summary>
        public static void OnStatusChanged(MatcBuddyContext context, Match match, byte status)
        {
            if (match.SeriesId == null) return;
            var series = context.MatchSeries.FirstOrDefault(s => s.SeriesId == match.SeriesId);
            if (series == null) return;
            if (status == MatchStatus.Approved) series.Approved = true;
            else if (status == MatchStatus.Rejected && !series.Approved)
            {
                series.IsActive = false;
                series.StoppedAt = DateTime.Now;
            }
        }

        public static bool IsActive(MatcBuddyContext context, int? seriesId) =>
            seriesId.HasValue && context.MatchSeries.AsNoTracking().Any(s => s.SeriesId == seriesId && s.IsActive);

        public static HashSet<int> ActiveIds(MatcBuddyContext context, IEnumerable<int?> seriesIds)
        {
            var ids = seriesIds.Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();
            if (ids.Count == 0) return new();
            return context.MatchSeries.AsNoTracking().Where(s => ids.Contains(s.SeriesId) && s.IsActive)
                .Select(s => s.SeriesId).ToHashSet();
        }

        /// <summary>Tekrarı durdur: maçın kurucusu, tesisin işletmesi ya da admin. Oluşmuş haftalar yerinde kalır.</summary>
        public static OperationResult<bool> Stop(int matchId, int actorId, bool isAdmin)
        {
            using var context = new MatcBuddyContext();
            var match = context.Matchs.AsNoTracking().FirstOrDefault(m => m.MatchId == matchId);
            if (match == null) return OperationResult<bool>.NotFound(ErrorCodes.Match.NotFound);
            var series = match.SeriesId == null ? null : context.MatchSeries.FirstOrDefault(s => s.SeriesId == match.SeriesId);
            if (series == null || !series.IsActive) return OperationResult<bool>.BadRequest(ErrorCodes.Match.NotRepeating);
            var ownerId = context.Stadiums.AsNoTracking().Where(s => s.StadiumId == match.StadiumId).Select(s => s.OwnerPlayerId).FirstOrDefault();
            if (!isAdmin && series.CreatedByPlayerId != actorId && ownerId != actorId)
                return OperationResult<bool>.Forbidden(ErrorCodes.Match.OrganizerOnly);
            series.IsActive = false;
            series.StoppedAt = DateTime.Now;
            context.SaveChanges();
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Growth.RepeatStopped));
        }

        /// <summary>Vadesi gelen haftaları oluşturur (arka plan, MatchReminderService). Döner: oluşturulan maç sayısı.</summary>
        public static int Run()
        {
            var lead = DateTime.Now.AddDays(P.Int(ParameterKeys.Venue.SeriesLeadDays));
            List<int> due;
            using (var context = new MatcBuddyContext())
            {
                due = context.MatchSeries.AsNoTracking().Where(s => s.IsActive && s.NextDate <= lead)
                    .Select(s => s.SeriesId).ToList();
            }
            var created = 0;
            foreach (var id in due)
            {
                try
                {
                    if (CreateNext(id)) created++;
                }
                catch
                {
                    // Bir serinin hatası diğerlerini durdurmasın; bir sonraki turda tekrar denenir
                }
            }
            return created;
        }

        private static bool CreateNext(int seriesId)
        {
            using var context = new MatcBuddyContext();
            using var transaction = context.Database.BeginTransaction();
            var series = context.MatchSeries.FirstOrDefault(s => s.SeriesId == seriesId && s.IsActive);
            if (series == null) return false;

            // Şablon: serinin en son haftası (reddedilmemiş). Hiç maç kalmadıysa seri biter.
            var template = context.Matchs.AsNoTracking()
                .Where(m => m.SeriesId == seriesId && m.IsActive != MatchStatus.Rejected)
                .OrderByDescending(m => m.MatchDate).FirstOrDefault();
            var start = series.NextDate;
            series.NextDate = start.AddDays(7);
            if (template == null || start < DateTime.Now)
            {
                if (template == null)
                {
                    series.IsActive = false;
                    series.StoppedAt = DateTime.Now;
                }
                context.SaveChanges();
                transaction.Commit();
                return false;
            }

            var pitch = VenueRules.LockPitch(context, series.PitchId);
            var stadium = context.Stadiums.AsNoTracking().FirstOrDefault(s => s.StadiumId == series.StadiumId);
            var creator = context.Players.AsNoTracking().FirstOrDefault(p => p.PlayerId == series.CreatedByPlayerId && !p.IsDeleted);
            if (pitch == null || stadium == null || creator == null || !pitch.IsActive)
            {
                series.IsActive = false;
                series.StoppedAt = DateTime.Now;
                context.SaveChanges();
                transaction.Commit();
                return false;
            }

            var duration = template.DurationMinutes <= 0 ? 60 : template.DurationMinutes;
            var pricing = VenueRules.LoadPricing(context, stadium);
            if (VenueRules.CheckSlot(context, pricing, pitch, start, duration) != null)
            {
                // Saat dolu / kapalı: bu hafta atlanır, kurucuya haber verilir
                AddNotification(context, NotificationType.MatchRejection,
                    Msg.GetDefault(MessageKeys.Growth.NotifSeriesSkipped, template.MatchName, start), template.MatchId,
                    new[] { series.CreatedByPlayerId });
                context.SaveChanges();
                transaction.Commit();
                return false;
            }

            var quote = VenueRules.BuildQuote(context, pricing, pitch, start, duration, template.UserCount, 0, template.CameraRequested);
            var status = series.Approved ? MatchStatus.Approved : MatchStatus.Pending;
            var match = new Match
            {
                MatchName = template.MatchName,
                Description = template.Description,
                MatchDate = start,
                UserCount = template.UserCount,
                IsActive = status,
                StadiumId = stadium.StadiumId,
                PitchId = pitch.PitchId,
                DurationMinutes = duration,
                PitchPrice = quote.PitchTotal,
                ExtrasPrice = quote.ExtrasTotal,
                TotalPrice = quote.Total,
                CameraRequested = template.CameraRequested,
                HomeTeamId = template.HomeTeamId,
                AwayTeamId = template.AwayTeamId,
                MinReliability = template.MinReliability,
                CreatedByPlayerId = series.CreatedByPlayerId,
                SeriesId = series.SeriesId,
                CreatedAt = DateTime.Now,
            };
            context.Matchs.Add(match);
            context.SaveChanges();

            context.MatchParticipants.Add(new MatchParticipant
            {
                MatchId = match.MatchId,
                PlayerId = series.CreatedByPlayerId,
                Status = ParticipantStatus.Going,
                Side = ParticipantSide.Home,
                Confirmed = true,
                JoinedAt = DateTime.Now,
            });
            foreach (var teamId in new[] { template.HomeTeamId, template.AwayTeamId }.Where(t => t.HasValue))
                context.MatchTeam.Add(new MatchTeam { MatchId = match.MatchId, TeamId = teamId!.Value });

            // Geçen haftanın oyuncularına davet: kabul eden kadroya girer
            var previous = context.MatchParticipants.AsNoTracking()
                .Where(p => p.MatchId == template.MatchId && p.PlayerId != series.CreatedByPlayerId
                            && (p.Status == ParticipantStatus.Going || p.Status == ParticipantStatus.Waitlist))
                .Select(p => new { p.PlayerId, p.Side })
                .ToList();
            foreach (var p in previous)
            {
                context.MatchInvites.Add(new MatchInvite
                {
                    MatchId = match.MatchId, InviterId = series.CreatedByPlayerId, InviteeId = p.PlayerId,
                    Status = InviteStatus.Pending, CreatedAt = DateTime.Now,
                    Side = template.HomeTeamId.HasValue || template.AwayTeamId.HasValue ? p.Side : null,
                });
            }
            if (previous.Count > 0)
            {
                NotificationWriter.Add(context, NotificationType.MatchInvite,
                    Msg.GetDefault(MessageKeys.Growth.NotifSeriesInvite, match.MatchName, start), match.MatchId, null,
                    previous.Select(p => p.PlayerId));
            }
            AddNotification(context, NotificationType.MatchCreated,
                Msg.GetDefault(MessageKeys.Growth.NotifSeriesCreated, match.MatchName, start), match.MatchId,
                new[] { series.CreatedByPlayerId });
            if (stadium.OwnerPlayerId.HasValue)
            {
                NotificationWriter.Add(context, NotificationType.ReservationRequest,
                    Msg.GetDefault(status == MatchStatus.Approved ? MessageKeys.Growth.NotifSeriesOwnerAuto : MessageKeys.Growth.NotifSeriesOwnerPending,
                        match.MatchName, start, pitch.Name),
                    match.MatchId, stadium.StadiumId, new[] { stadium.OwnerPlayerId.Value });
            }
            AddActivity(context, ActivityType.MatchCreated, series.CreatedByPlayerId, matchId: match.MatchId);

            context.SaveChanges();
            transaction.Commit();
            return true;
        }
    }
}
