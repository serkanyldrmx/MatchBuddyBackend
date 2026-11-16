using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using static MatcBuddy.DataAccess.Concrete.EntityFramework.ParticipationCore;

namespace MatcBuddy.DataAccess.Concrete.EntityFramework
{
    /// <summary>
    /// İşletmenin onaylı rezervasyon işlemleri (iptal, tahsilat / gelmedi işareti), bekleyen taleplerin otomatik
    /// düşmesi ve kurucunun ücretsiz iptal süresi içinde vazgeçmesi ("geç iptal").
    /// </summary>
    public partial class EFVenueDal
    {
        public const int CancelReasonMaxLength = 200;

        // Rezervasyonu (maçı) kilitleyip tesisine sahiplik + üyelik kontrolü
        private static (Match? Match, Stadium? Stadium, AppError? Error) LoadOwnedMatch(MatcBuddyContext context, int me, int matchId)
        {
            var match = LockMatch(context, matchId);
            if (match == null || match.PitchId == null) return (null, null, AppError.NotFound(ErrorCodes.Match.NotFound));
            var stadium = StadiumOf(context, match.StadiumId);
            var g = Guard(context, me, stadium, true);
            return g != null ? (null, null, g) : (match, stadium, null);
        }

        /// <summary>
        /// İşletme onaylı (henüz başlamamış) bir rezervasyonu sebebiyle iptal eder: maç reddedildi (3) + RejectReason +
        /// CancelledAt olur, saat boşalır; kurucu, kadro ve yedekler bilgilendirilir. Haftalık seri devam eder
        /// (yalnızca bu hafta iptal); seriyi durdurmak için Match/StopRepeat.
        /// </summary>
        public OperationResult<bool> CancelReservation(int me, int matchId, string reason)
        {
            using var context = new MatcBuddyContext();
            using var transaction = context.Database.BeginTransaction();
            var (match, stadium, error) = LoadOwnedMatch(context, me, matchId);
            if (error != null) return Fail<bool>(error);
            if (match!.IsActive != MatchStatus.Approved)
                return OperationResult<bool>.BadRequest(ErrorCodes.Reservation.NotApproved);
            if (match.MatchDate == null || match.MatchDate.Value <= DateTime.Now)
                return OperationResult<bool>.BadRequest(ErrorCodes.Reservation.CancelStarted);

            match.IsActive = MatchStatus.Rejected;
            match.RejectReason = reason;
            match.CancelledAt = DateTime.Now;
            MatchSeriesService.OnStatusChanged(context, match, MatchStatus.Rejected);
            var place = stadium?.StadiumName ?? Msg.GetDefault(MessageKeys.Reservation.LabelVenueFallback);
            NotifyReservationStatus(context, match, stadium, MatchStatus.Rejected, reason,
                Msg.GetDefault(MessageKeys.Reservation.NotifCancelledByVenue, match.MatchName, match.MatchDate, place, reason),
                includeWaitlist: true);

            context.SaveChanges();
            transaction.Commit();
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Owner.ReservationCancelled));
        }

        /// <summary>
        /// Uygulama rezervasyonunun tahsilat / gelmedi işareti (yalnızca onaylı maçlar). null alanlar değişmez.
        /// Gelmedi yalnızca başlangıçtan sonra işaretlenebilir. Finans: tahsil edilen = IsPaid, NoShow gelire girmez.
        /// </summary>
        public OperationResult<bool> SetReservationPayment(int me, int matchId, bool? isPaid, bool? noShow)
        {
            using var context = new MatcBuddyContext();
            using var transaction = context.Database.BeginTransaction();
            var (match, _, error) = LoadOwnedMatch(context, me, matchId);
            if (error != null) return Fail<bool>(error);
            if (match!.IsActive != MatchStatus.Approved)
                return OperationResult<bool>.BadRequest(ErrorCodes.Reservation.PaymentOnlyApproved);
            if (noShow == true && (match.MatchDate == null || match.MatchDate.Value > DateTime.Now))
                return OperationResult<bool>.BadRequest(ErrorCodes.Reservation.NoShowBeforeStart);

            if (isPaid.HasValue) match.IsPaid = isPaid.Value;
            if (noShow.HasValue) match.NoShow = noShow.Value;
            context.SaveChanges();
            transaction.Commit();

            var message = noShow.HasValue
                ? (noShow.Value ? MessageKeys.Owner.NoShowMarked : MessageKeys.Owner.NoShowCleared)
                : (match.IsPaid ? MessageKeys.Owner.PaymentReceived : MessageKeys.Owner.PaymentPending);
            return OperationResult<bool>.Ok(true, Msg.Get(message));
        }

        // ------------------------------------------------------------------ Bekleyen taleplerin düşmesi

        /// <summary>
        /// Arka plan (MatchReminderService): booking.pendingExpiryHours saatten uzun bekleyen ya da başlangıcı gelmiş
        /// talepleri sebebiyle reddeder, kurucu ve kadroya bildirir. Onaylanmamış serinin ilk talebi düşerse seri durur.
        /// Döner: düşen talep sayısı.
        /// </summary>
        public static int ExpirePendingReservations()
        {
            var now = DateTime.Now;
            var hours = P.Int(ParameterKeys.Booking.PendingExpiryHours);
            var createdBefore = now.AddHours(-hours);
            List<int> due;
            using (var context = new MatcBuddyContext())
            {
                due = context.Matchs.AsNoTracking()
                    .Where(m => m.IsActive == MatchStatus.Pending && m.PitchId != null
                                && (m.MatchDate == null || m.MatchDate <= now || m.CreatedAt <= createdBefore))
                    .Select(m => m.MatchId).ToList();
            }

            var expired = 0;
            foreach (var id in due)
            {
                try
                {
                    if (ExpireOne(id, hours)) expired++;
                }
                catch
                {
                    // Bir talebin hatası diğerlerini durdurmasın; bir sonraki turda tekrar denenir
                }
            }
            return expired;
        }

        private static bool ExpireOne(int matchId, int hours)
        {
            using var context = new MatcBuddyContext();
            using var transaction = context.Database.BeginTransaction();
            var match = LockMatch(context, matchId);
            var now = DateTime.Now;
            // Kilit alınana kadar işletme yanıtlamış olabilir
            if (match == null || match.IsActive != MatchStatus.Pending
                || !VenueBookingRules.IsPendingExpired(match.MatchDate, match.CreatedAt, now, hours))
                return false;

            var startPassed = match.MatchDate == null || match.MatchDate.Value <= now;
            var reason = startPassed
                ? Msg.GetDefault(MessageKeys.Reservation.ExpiredStartPassed)
                : Msg.GetDefault(MessageKeys.Reservation.ExpiredNoResponse, hours);
            if (reason.Length > CancelReasonMaxLength) reason = reason[..CancelReasonMaxLength];

            match.IsActive = MatchStatus.Rejected;
            match.RejectReason = reason;
            MatchSeriesService.OnStatusChanged(context, match, MatchStatus.Rejected);
            var stadium = context.Stadiums.AsNoTracking().FirstOrDefault(s => s.StadiumId == match.StadiumId);
            NotifyReservationStatus(context, match, stadium, MatchStatus.Rejected, reason);

            context.SaveChanges();
            transaction.Commit();
            return true;
        }

        // ------------------------------------------------------------------ Geç iptal (oyuncu tarafı çağırır)

        /// <summary>
        /// Kurucu, onaylı bir tesis rezervasyonunu tesisin ücretsiz iptal süresi (VenueSettings.FreeCancellationHours)
        /// içinde iptal ettiyse (sildi) ya da maçtan çekildiyse işletmeye "geç iptal" bildirimi yazar. Maç satırı
        /// kalıyorsa (çekilme) Match.LateCancelledAt işaretlenir. Kurucu dışındaki oyuncuların çekilmesi tesisi
        /// ilgilendirmez (kadro tamamlanır), bildirilmez. SaveChanges çağırmaz. Döner: geç iptal sayıldı mı.
        /// </summary>
        public static bool NotifyLateCancel(MatcBuddyContext context, Match match, int actorPlayerId, bool deleted)
        {
            if (match.PitchId == null || match.IsActive != MatchStatus.Approved) return false;
            if (match.CreatedByPlayerId != actorPlayerId) return false;
            var stadium = context.Stadiums.AsNoTracking().FirstOrDefault(s => s.StadiumId == match.StadiumId);
            if (stadium?.OwnerPlayerId == null) return false;
            var hours = VenueRules.EffectiveSettings(context, stadium.StadiumId).FreeCancellationHours;
            if (!VenueBookingRules.IsLateCancel(match.MatchDate, DateTime.Now, hours)) return false;

            var actor = context.Players.AsNoTracking().FirstOrDefault(p => p.PlayerId == actorPlayerId);
            var text = Msg.GetDefault(deleted ? MessageKeys.Reservation.NotifLateCancelDeleted : MessageKeys.Reservation.NotifLateCancelLeft,
                DisplayName(actor), match.MatchName, match.MatchDate, stadium.StadiumName, hours);
            // Silinen maçın bildirimi maça bağlanmaz (MatchId = null)
            NotificationWriter.Add(context, NotificationType.MatchRejection, text, deleted ? null : match.MatchId, stadium.StadiumId,
                new[] { stadium.OwnerPlayerId.Value });
            if (!deleted) match.LateCancelledAt = DateTime.Now;
            return true;
        }
    }
}
