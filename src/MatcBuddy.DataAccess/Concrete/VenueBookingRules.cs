namespace MatcBuddy.DataAccess.Concrete
{
    /// <summary>
    /// Rezervasyon zaman kuralları (veritabanına dokunmaz; birim testi için VenuePricing gibi ayrı tutuldu):
    /// haftalık seri ("abone" takım) için ileri haftaların dolu sayılması, bekleyen taleplerin süresinin dolması
    /// ve ücretsiz iptal süresi içinde yapılan "geç iptal".
    /// </summary>
    public static class VenueBookingRules
    {
        /// <summary>
        /// Aktif bir haftalık serinin henüz oluşturulmamış haftaları: nextDate, nextDate + 7g, ... Yalnızca
        /// [from, to) ile çakışanlar döner. Böylece abonenin gelecek haftaları başka rezervasyonlara kapalı kalır.
        /// </summary>
        public static IEnumerable<(DateTime Start, DateTime End)> SeriesOccurrences(DateTime nextDate, int durationMinutes, DateTime from, DateTime to)
        {
            if (to <= from) yield break;
            var duration = TimeSpan.FromMinutes(durationMinutes <= 0 ? 60 : durationMinutes);
            var week = TimeSpan.FromDays(7);
            var start = nextDate;
            if (start + duration <= from)
            {
                // from'dan hemen önceki haftaya atla (çakışma kontrolü aşağıda)
                var weeks = (long)Math.Floor((from - nextDate).TotalDays / 7) - 1;
                if (weeks > 0) start = nextDate.AddDays(7 * weeks);
            }
            for (; start < to; start += week)
            {
                var end = start + duration;
                if (end > from) yield return (start, end);
            }
        }

        /// <summary>
        /// Bekleyen talep düşmeli mi: başlangıcı geçti (ya da başlangıç yok) veya talep expiryHours saatten uzun süredir bekliyor.
        /// </summary>
        public static bool IsPendingExpired(DateTime? start, DateTime createdAt, DateTime now, int expiryHours)
        {
            if (start == null || start.Value <= now) return true;
            return expiryHours > 0 && createdAt != default && createdAt.AddHours(expiryHours) <= now;
        }

        /// <summary>
        /// Geç iptal: maç henüz başlamadı ama başlangıca freeCancellationHours saatten az kaldı.
        /// freeCancellationHours 0 ise ücretsiz iptal her zaman serbest (geç iptal yok).
        /// </summary>
        public static bool IsLateCancel(DateTime? start, DateTime now, int freeCancellationHours)
        {
            if (start == null || freeCancellationHours <= 0) return false;
            return start.Value > now && start.Value - now < TimeSpan.FromHours(freeCancellationHours);
        }
    }
}
