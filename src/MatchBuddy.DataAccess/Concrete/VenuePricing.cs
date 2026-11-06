using System.Globalization;
using MatchBuddy.Entities.Entity;

namespace MatchBuddy.DataAccess.Concrete
{
    /// <summary>
    /// Tesis açılış saatleri, dilim üretimi, fiyat kuralı / kampanya çözümü ve fiyat teklifi hesabı
    /// (docs/venues-api.md "Fiyat hesabı" ve "Müsaitlik kuralları"). Veritabanına dokunmaz; yalnızca
    /// bellekteki nesneler üzerinde çalışır (birim testi kolay olsun diye ayrı tutuldu). Deterministiktir.
    /// </summary>
    public static class VenuePricing
    {
        // Sistem parametreleri (docs/platform-api.md): venue.allowedDurations, venue.bookingMinPlayersPerSide, venue.extraPlayersAllowed
        public static IReadOnlyList<int> AllowedDurations => P.Ints(ParameterKeys.Venue.AllowedDurations);
        public static int MinPlayersPerSide => P.Int(ParameterKeys.Venue.BookingMinPlayersPerSide); // userCount en az 2 × bu
        public static int ExtraPlayers => P.Int(ParameterKeys.Venue.ExtraPlayersAllowed);           // userCount en çok PlayersPerSide * 2 + bu

        // Varsayılan açılış saatleri (kayıt ve Stadiums.OpeningTime/ClosingTime yoksa): 09:00–00:00
        public static readonly TimeSpan DefaultOpen = new(9, 0, 0);
        public static readonly TimeSpan DefaultClose = TimeSpan.Zero;

        // ------------------------------------------------------------------ Gün / saat

        /// <summary>0 Pazartesi … 6 Pazar.</summary>
        public static int DayIndex(DateTime date) => ((int)date.DayOfWeek + 6) % 7;

        public static bool DayInMask(int mask, int dayIndex) => (mask & (1 << dayIndex)) != 0;

        public static bool Overlaps(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd) =>
            aStart < bEnd && bStart < aEnd;

        public static string FormatTime(TimeSpan t) => $"{t.Hours:00}:{t.Minutes:00}";

        public static bool TryParseTime(string? value, out TimeSpan time)
        {
            time = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(value)) return false;
            var v = value.Trim();
            if (v == "24:00") { time = TimeSpan.Zero; return true; }
            if (TimeSpan.TryParseExact(v, new[] { @"hh\:mm", @"h\:mm", @"hh\:mm\:ss" }, CultureInfo.InvariantCulture, out var t)
                && t >= TimeSpan.Zero && t < TimeSpan.FromDays(1))
            {
                time = t;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ Açılış saatleri

        public sealed record DayHours(int DayOfWeek, bool IsClosed, TimeSpan Open, TimeSpan Close);

        /// <summary>
        /// Haftanın 7 günü (0 Pazartesi … 6 Pazar). Kaydı olmayan gün için Stadiums.OpeningTime/ClosingTime,
        /// onlar da yoksa 09:00–00:00 kullanılır.
        /// </summary>
        public static List<DayHours> WeekHours(IEnumerable<OpeningHour> records, TimeOnly? fallbackOpen, TimeOnly? fallbackClose)
        {
            var byDay = records.GroupBy(r => (int)r.DayOfWeek).ToDictionary(g => g.Key, g => g.First());
            var open = fallbackOpen?.ToTimeSpan() ?? DefaultOpen;
            var close = fallbackClose?.ToTimeSpan() ?? DefaultClose;
            if (fallbackOpen == null || fallbackClose == null)
            {
                open = DefaultOpen;
                close = DefaultClose;
            }
            var list = new List<DayHours>();
            for (var d = 0; d < 7; d++)
            {
                list.Add(byDay.TryGetValue(d, out var r)
                    ? new DayHours(d, r.IsClosed, r.OpenTime, r.CloseTime)
                    : new DayHours(d, false, open, close));
            }
            return list;
        }

        /// <summary>
        /// Takvim gününün iş günü penceresi [açılış, kapanış). Kapanış &lt;= açılış ise ertesi güne taşar
        /// (00:00 = gece yarısı; açılış = kapanış = 24 saat açık). Kapalı gün: null.
        /// </summary>
        public static (DateTime Start, DateTime End)? Window(DateTime date, IReadOnlyList<DayHours> week)
        {
            var day = week[DayIndex(date)];
            if (day.IsClosed) return null;
            var start = date.Date + day.Open;
            var end = date.Date + day.Close;
            if (end <= start) end = end.AddDays(1);
            return (start, end);
        }

        /// <summary>[start, end) tek bir açılış penceresine (o günün ya da önceki günün taşan) tamamen sığıyor mu?</summary>
        public static bool IsWithinOpening(DateTime start, DateTime end, IReadOnlyList<DayHours> week)
        {
            foreach (var date in new[] { start.Date, start.Date.AddDays(-1) })
            {
                var w = Window(date, week);
                if (w != null && start >= w.Value.Start && end <= w.Value.End) return true;
            }
            return false;
        }

        /// <summary>Günün saatlik dilimleri: açılıştan başlayıp kapanışa sığan her tam saat (gece yarısını aşabilir).</summary>
        public static List<(DateTime Start, DateTime End)> DaySlots(DateTime date, IReadOnlyList<DayHours> week)
        {
            var result = new List<(DateTime, DateTime)>();
            var w = Window(date, week);
            if (w == null) return result;
            for (var s = w.Value.Start; s.AddHours(1) <= w.Value.End; s = s.AddHours(1))
            {
                result.Add((s, s.AddHours(1)));
            }
            return result;
        }

        /// <summary>Haftalık toplam açık dakika (doluluk oranı paydası için, saha başına).</summary>
        public static double OpenMinutesBetween(DateTime from, DateTime to, IReadOnlyList<DayHours> week)
        {
            double total = 0;
            for (var d = from.Date.AddDays(-1); d < to; d = d.AddDays(1))
            {
                var w = Window(d, week);
                if (w == null) continue;
                var s = w.Value.Start < from ? from : w.Value.Start;
                var e = w.Value.End > to ? to : w.Value.End;
                if (e > s) total += (e - s).TotalMinutes;
            }
            return total;
        }

        // ------------------------------------------------------------------ Fiyat

        public sealed record HourPrice(decimal Price, decimal OriginalPrice, string? CampaignTitle, int? CampaignId);

        public static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

        /// <summary>
        /// Saatin taban ücreti: o saati kapsayan kural (sahaya özel kural genele göre öncelikli; aynı öncelikte
        /// daha dar saat aralığı, sonra daha yeni kural), yoksa Pitch.BasePrice.
        /// </summary>
        public static decimal BaseHourPrice(DateTime hourStart, Pitch pitch, IEnumerable<PriceRule> rules)
        {
            var day = DayIndex(hourStart);
            var hour = hourStart.Hour;
            var rule = rules
                .Where(r => r.StadiumId == pitch.StadiumId
                            && (r.PitchId == null || r.PitchId == pitch.PitchId)
                            && DayInMask(r.DaysMask, day)
                            && r.FromHour <= hour && hour < r.ToHour)
                .OrderByDescending(r => r.PitchId.HasValue)
                .ThenBy(r => r.ToHour - r.FromHour)
                .ThenByDescending(r => r.PriceRuleId)
                .FirstOrDefault();
            return rule?.PricePerHour ?? pitch.BasePrice;
        }

        /// <summary>Kampanya bu saate (tarih aralığı, gün, saat, saha) uyuyor mu?</summary>
        public static bool CampaignApplies(Campaign c, DateTime hourStart, Pitch pitch)
        {
            if (!c.IsActive || c.StadiumId != pitch.StadiumId) return false;
            if (c.PitchId.HasValue && c.PitchId.Value != pitch.PitchId) return false;
            var date = hourStart.Date;
            if (date < c.StartDate.Date || date > c.EndDate.Date) return false;
            if (!DayInMask(c.DaysMask, DayIndex(hourStart))) return false;
            return c.FromHour <= hourStart.Hour && hourStart.Hour < c.ToHour;
        }

        public static decimal CampaignPrice(Campaign c, decimal basePrice)
        {
            if (c.DiscountPercent.HasValue) return Round2(basePrice * (100 - c.DiscountPercent.Value) / 100m);
            return c.FixedPrice.HasValue ? Round2(c.FixedPrice.Value) : basePrice;
        }

        /// <summary>Saatin uygulanan ücreti: uyan kampanyalardan en ucuz sonucu veren (taban ücretten ucuzsa).</summary>
        public static HourPrice PriceForHour(DateTime hourStart, Pitch pitch, IEnumerable<PriceRule> rules, IEnumerable<Campaign> campaigns)
        {
            hourStart = new DateTime(hourStart.Year, hourStart.Month, hourStart.Day, hourStart.Hour, 0, 0);
            var basePrice = BaseHourPrice(hourStart, pitch, rules);
            var best = campaigns
                .Where(c => CampaignApplies(c, hourStart, pitch))
                .Select(c => new { Campaign = c, Price = CampaignPrice(c, basePrice) })
                .OrderBy(x => x.Price)
                .ThenBy(x => x.Campaign.CampaignId)
                .FirstOrDefault();
            if (best == null || best.Price >= basePrice) return new HourPrice(basePrice, basePrice, null, null);
            return new HourPrice(best.Price, basePrice, best.Campaign.Title, best.Campaign.CampaignId);
        }

        public sealed record QuoteLine(DateTime Start, DateTime End, string Label, decimal Price, decimal OriginalPrice, string? CampaignTitle);

        /// <summary>[start, start + duration) aralığını saat dilimlerine böler; her dilime o saatin ücreti oranlı uygulanır.</summary>
        public static List<QuoteLine> Lines(DateTime start, int durationMinutes, Pitch pitch, IEnumerable<PriceRule> rules, IEnumerable<Campaign> campaigns)
        {
            var ruleList = rules.ToList();
            var campaignList = campaigns.ToList();
            var lines = new List<QuoteLine>();
            var end = start.AddMinutes(durationMinutes);
            var cursor = start;
            while (cursor < end)
            {
                var hourStart = new DateTime(cursor.Year, cursor.Month, cursor.Day, cursor.Hour, 0, 0);
                var next = hourStart.AddHours(1);
                if (next > end) next = end;
                var fraction = (decimal)(next - cursor).TotalMinutes / 60m;
                var hp = PriceForHour(hourStart, pitch, ruleList, campaignList);
                lines.Add(new QuoteLine(cursor, next, $"{cursor:HH:mm}–{next:HH:mm}",
                    Round2(hp.Price * fraction), Round2(hp.OriginalPrice * fraction), hp.CampaignTitle));
                cursor = next;
            }
            return lines;
        }

        /// <summary>Krampon (adet × ücret) + kamera (istendiyse) ücreti.</summary>
        public static decimal Extras(Stadium stadium, int cleatCount, bool cameraRequested)
        {
            var total = 0m;
            if (cleatCount > 0) total += cleatCount * (stadium.CleatRentalPrice ?? 0m);
            if (cameraRequested) total += stadium.CameraPrice ?? 0m;
            return Round2(total);
        }

        /// <summary>Kişi başı = ceil(toplam / oyuncu sayısı).</summary>
        public static decimal PerPlayer(decimal total, int userCount) =>
            userCount <= 0 ? Math.Ceiling(total) : Math.Ceiling(total / userCount);

        public static int MinUserCount() => 2 * MinPlayersPerSide;
        public static int MaxUserCount(int playersPerSide) => playersPerSide * 2 + ExtraPlayers;

        /// <summary>Liste için en düşük saatlik ücret: aktif sahaların taban ücreti ve onlara uygulanabilen kurallar.</summary>
        public static decimal? MinPrice(IEnumerable<Pitch> activePitches, IEnumerable<PriceRule> rules)
        {
            var pitches = activePitches.ToList();
            if (pitches.Count == 0) return null;
            var prices = pitches.Select(p => p.BasePrice).ToList();
            foreach (var r in rules)
            {
                if (r.PitchId == null || pitches.Any(p => p.PitchId == r.PitchId)) prices.Add(r.PricePerHour);
            }
            return prices.Min();
        }
    }

    /// <summary>"lat, lon" konum metni ve mesafe yardımcıları.</summary>
    public static class VenueGeo
    {
        public static bool TryParseLocation(string? location, out double lat, out double lon)
        {
            lat = lon = 0;
            if (string.IsNullOrWhiteSpace(location)) return false;
            var parts = location.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length != 2) return false;
            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out lat)) return false;
            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out lon)) return false;
            return lat is >= -90 and <= 90 && lon is >= -180 and <= 180 && !(lat == 0 && lon == 0);
        }

        public static string Format(double lat, double lon) =>
            string.Create(CultureInfo.InvariantCulture, $"{lat:0.######}, {lon:0.######}");

        public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double r = 6371000;
            double ToRad(double d) => d * Math.PI / 180;
            var dLat = ToRad(lat2 - lat1);
            var dLon = ToRad(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return 2 * r * Math.Asin(Math.Sqrt(a));
        }
    }
}
