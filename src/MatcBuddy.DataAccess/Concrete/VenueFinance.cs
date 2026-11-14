using System.Globalization;
using MatcBuddy.Entities.DTOs;

namespace MatcBuddy.DataAccess.Concrete
{
    /// <summary>
    /// İşletme gelir-gider özeti (docs/venues-api.md "Ek paket" §3–4). Veritabanına dokunmaz; bellekteki
    /// kayıtlar ve "şimdi" üzerinden hesaplar (birim testi kolay olsun diye VenuePricing gibi ayrı tutuldu).
    ///
    /// Kurallar:
    /// - Uygulama maçı geliri: onaylı (IsActive 2), PitchId dolu ve bitmiş (start + süre ≤ şimdi) maçların TotalPrice'ı.
    /// - Harici rezervasyon geliri: Kind 1, bitmiş ve ödenmiş blokların Price'ı.
    /// - Tahsil edilmemiş (receivable): Kind 1, bitmiş ve ödenmemiş blokların Price'ı (seçili ay).
    /// - Beklenen gelir: seçili ayda henüz bitmemiş onaylı maçlar + bitmemiş harici rezervasyonlar.
    /// - Gelir, başlangıç tarihinin ayına (ve gününe) yazılır. Giderler kendi tarihine.
    /// Çağıran sadece onaylı ve PitchId dolu maçları, sadece Kind 1 blokları verir.
    /// </summary>
    public static class VenueFinance
    {
        public sealed record FinanceMatch(int MatchId, int PitchId, string? Title, DateTime Start, int DurationMinutes, decimal Total)
        {
            public DateTime End => Start.AddMinutes(DurationMinutes <= 0 ? 60 : DurationMinutes);
        }

        public sealed record FinanceBooking(int SlotBlockId, int PitchId, DateTime Start, DateTime End, decimal Price, bool IsPaid,
            string? CustomerName, string? Reason);

        public sealed record FinanceExpense(int ExpenseId, DateTime Date, string Category, decimal Amount, string? Description);

        public sealed record FinancePitch(int PitchId, string Name);

        public static DateTime MonthStart(int year, int month) => new(year, month, 1);

        private static bool InMonth(DateTime d, DateTime monthStart) => d >= monthStart && d < monthStart.AddMonths(1);

        /// <summary>Ay içindeki gerçekleşmiş gelir (maç + ödenmiş harici) ve gider.</summary>
        public static (decimal Matches, decimal Manual, decimal Expenses) MonthTotals(DateTime monthStart, DateTime now,
            IEnumerable<FinanceMatch> matches, IEnumerable<FinanceBooking> bookings, IEnumerable<FinanceExpense> expenses)
        {
            var m = matches.Where(x => InMonth(x.Start, monthStart) && x.End <= now).Sum(x => x.Total);
            var b = bookings.Where(x => InMonth(x.Start, monthStart) && x.End <= now && x.IsPaid).Sum(x => x.Price);
            var e = expenses.Where(x => InMonth(x.Date.Date, monthStart)).Sum(x => x.Amount);
            return (m, b, e);
        }

        public static string BookingTitle(string? reason, string? customerName)
        {
            var r = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            var c = string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim();
            if (r != null && c != null) return $"{r} – {c}";
            return r ?? c ?? Msg.Get(MessageKeys.Venue.LabelExternalBooking);
        }

        public static OwnerFinanceDto Build(int year, int month, DateTime now,
            IReadOnlyCollection<FinanceMatch> matches, IReadOnlyCollection<FinanceBooking> bookings,
            IReadOnlyCollection<FinanceExpense> expenses, IReadOnlyCollection<FinancePitch> pitches)
        {
            var start = MonthStart(year, month);
            var end = start.AddMonths(1);
            var pitchNames = pitches.GroupBy(p => p.PitchId).ToDictionary(g => g.Key, g => g.First().Name);

            var monthMatches = matches.Where(x => InMonth(x.Start, start)).ToList();
            var monthBookings = bookings.Where(x => InMonth(x.Start, start)).ToList();
            var monthExpenses = expenses.Where(x => InMonth(x.Date.Date, start)).ToList();

            var doneMatches = monthMatches.Where(x => x.End <= now).ToList();
            var doneBookings = monthBookings.Where(x => x.End <= now).ToList();
            var paidBookings = doneBookings.Where(x => x.IsPaid).ToList();

            var incomeMatches = doneMatches.Sum(x => x.Total);
            var incomeManual = paidBookings.Sum(x => x.Price);
            var incomeTotal = incomeMatches + incomeManual;
            var expenseTotal = monthExpenses.Sum(x => x.Amount);
            var net = incomeTotal - expenseTotal;

            var prevStart = start.AddMonths(-1);
            var prev = MonthTotals(prevStart, now, matches, bookings, expenses);

            var dto = new OwnerFinanceDto
            {
                Year = year,
                Month = month,
                Income = new FinanceIncomeDto { Matches = incomeMatches, Manual = incomeManual, Total = incomeTotal },
                Receivable = doneBookings.Where(x => !x.IsPaid).Sum(x => x.Price),
                ExpectedIncome = monthMatches.Where(x => x.End > now).Sum(x => x.Total)
                                 + monthBookings.Where(x => x.End > now).Sum(x => x.Price),
                Expenses = expenseTotal,
                Net = net,
                MarginPercent = incomeTotal > 0 ? (double)Math.Round(net * 100m / incomeTotal, 1, MidpointRounding.AwayFromZero) : 0,
                Previous = new FinancePeriodDto
                {
                    Income = prev.Matches + prev.Manual,
                    Expenses = prev.Expenses,
                    Net = prev.Matches + prev.Manual - prev.Expenses,
                },
                Counts = new FinanceCountsDto
                {
                    Matches = doneMatches.Count,
                    ManualBookings = doneBookings.Count,
                    HoursSold = Math.Round(doneMatches.Sum(x => (x.End - x.Start).TotalHours) + doneBookings.Sum(x => (x.End - x.Start).TotalHours), 2),
                },
            };

            // Günlük seri: ayın her günü
            for (var d = start; d < end; d = d.AddDays(1))
            {
                var next = d.AddDays(1);
                dto.Daily.Add(new FinanceDailyDto
                {
                    Date = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Income = doneMatches.Where(x => x.Start >= d && x.Start < next).Sum(x => x.Total)
                             + paidBookings.Where(x => x.Start >= d && x.Start < next).Sum(x => x.Price),
                    Expense = monthExpenses.Where(x => x.Date.Date == d).Sum(x => x.Amount),
                });
            }

            // Saha bazında gelir (gerçekleşmiş) ve satılan saat (bitmiş maç + bitmiş harici rezervasyon)
            var pitchIds = pitches.Select(p => p.PitchId)
                .Concat(doneMatches.Select(x => x.PitchId)).Concat(doneBookings.Select(x => x.PitchId))
                .Distinct();
            dto.IncomeByPitch = pitchIds.Select(id => new FinancePitchDto
                {
                    PitchId = id,
                    Name = pitchNames.TryGetValue(id, out var n) ? n : null,
                    Income = doneMatches.Where(x => x.PitchId == id).Sum(x => x.Total) + paidBookings.Where(x => x.PitchId == id).Sum(x => x.Price),
                    Hours = Math.Round(doneMatches.Where(x => x.PitchId == id).Sum(x => (x.End - x.Start).TotalHours)
                                       + doneBookings.Where(x => x.PitchId == id).Sum(x => (x.End - x.Start).TotalHours), 2),
                })
                .OrderByDescending(x => x.Income).ThenBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.PitchId)
                .ToList();

            dto.IncomeBySource = new List<FinanceSourceDto>
            {
                new() { Source = "matches", Amount = incomeMatches },
                new() { Source = "manual", Amount = incomeManual },
            };

            dto.ExpenseByCategory = monthExpenses.GroupBy(x => x.Category)
                .Select(g => new FinanceCategoryDto { Category = g.Key, Amount = g.Sum(x => x.Amount) })
                .OrderByDescending(x => x.Amount).ThenBy(x => x.Category, StringComparer.Ordinal)
                .ToList();

            // Son 12 ay (seçili ay dahil), eskiden yeniye
            for (var i = 11; i >= 0; i--)
            {
                var ms = start.AddMonths(-i);
                var t = MonthTotals(ms, now, matches, bookings, expenses);
                dto.Monthly.Add(new FinanceMonthDto
                {
                    Year = ms.Year,
                    Month = ms.Month,
                    Income = t.Matches + t.Manual,
                    Expenses = t.Expenses,
                    Net = t.Matches + t.Manual - t.Expenses,
                });
            }

            // Seçili ayın tüm hareketleri: onaylı maçlar, harici rezervasyonlar (ödenmiş/ödenmemiş, bitmiş/gelecek), giderler
            var tx = new List<FinanceTransactionDto>();
            tx.AddRange(monthMatches.Select(x => new FinanceTransactionDto
            {
                Date = x.Start,
                Type = "income",
                Source = "match",
                Title = x.Title,
                PitchName = pitchNames.TryGetValue(x.PitchId, out var n) ? n : null,
                Amount = x.Total,
                IsPaid = x.End <= now, // uygulama maçı oynandığında tesiste tahsil edilmiş sayılır
                RefId = x.MatchId,
            }));
            tx.AddRange(monthBookings.Select(x => new FinanceTransactionDto
            {
                Date = x.Start,
                Type = "income",
                Source = "manual",
                Title = BookingTitle(x.Reason, x.CustomerName),
                PitchName = pitchNames.TryGetValue(x.PitchId, out var n) ? n : null,
                Amount = x.Price,
                IsPaid = x.IsPaid,
                RefId = x.SlotBlockId,
            }));
            tx.AddRange(monthExpenses.Select(x => new FinanceTransactionDto
            {
                Date = x.Date.Date,
                Type = "expense",
                Source = "expense",
                Title = string.IsNullOrWhiteSpace(x.Description) ? x.Category : x.Description,
                Category = x.Category,
                Amount = x.Amount,
                IsPaid = true,
                RefId = x.ExpenseId,
            }));
            dto.Transactions = tx.OrderByDescending(x => x.Date).ThenByDescending(x => x.RefId).ToList();
            return dto;
        }
    }
}
