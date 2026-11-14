using System.Globalization;
using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatcBuddy.DataAccess.Concrete.EntityFramework
{
    /// <summary>
    /// İşletme paneli ek paketi (docs/venues-api.md "Ek paket"): harici rezervasyon fiyat teklifi, giderler ve
    /// finans özeti. Hesap mantığı saf VenueFinance sınıfında; burası yükler ve sahiplik/üyelik kontrolü yapar.
    /// </summary>
    public partial class EFVenueDal
    {
        // ------------------------------------------------------------------ Owner/Quote

        /// <summary>İşletmenin kendi fiyat kuralları (+ aktif kampanyalar) ile saha ücreti. Görünürlük / müsaitlik aranmaz.</summary>
        public OperationResult<OwnerQuoteDto> OwnerQuote(int me, int pitchId, DateTime start, int durationMinutes)
        {
            using var context = new MatcBuddyContext();
            var pitch = context.Pitches.AsNoTracking().FirstOrDefault(p => p.PitchId == pitchId);
            if (pitch == null) return OperationResult<OwnerQuoteDto>.NotFound(VenueRules.PitchNotFound);
            var stadium = StadiumOf(context, pitch.StadiumId);
            var g = Guard(context, me, stadium, false);
            if (g != null) return Fail<OwnerQuoteDto>(g);

            var pricing = VenueRules.LoadPricing(context, stadium!);
            var lines = VenuePricing.Lines(start, durationMinutes, pitch, pricing.Rules, pricing.Campaigns);
            return OperationResult<OwnerQuoteDto>.Ok(new OwnerQuoteDto
            {
                PitchTotal = VenuePricing.Round2(lines.Sum(l => l.Price)),
                Lines = lines.Select(l => new QuoteLineDto
                {
                    Label = l.Label, Price = l.Price, OriginalPrice = l.OriginalPrice, CampaignTitle = l.CampaignTitle,
                }).ToList(),
            });
        }

        // ------------------------------------------------------------------ Giderler

        private static VenueExpenseDto ToDto(VenueExpense e) => new()
        {
            ExpenseId = e.ExpenseId,
            StadiumId = e.StadiumId,
            Date = e.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Category = e.Category,
            Amount = e.Amount,
            Description = e.Description,
            CreatedAt = e.CreatedAt,
        };

        /// <summary>Tarihe göre azalan. from / to tarih olarak dahil (gün bazında).</summary>
        public OperationResult<List<VenueExpenseDto>> Expenses(int me, int stadiumId, DateTime? from, DateTime? to)
        {
            using var context = new MatcBuddyContext();
            var g = Guard(context, me, StadiumOf(context, stadiumId), false);
            if (g != null) return Fail<List<VenueExpenseDto>>(g);

            var query = context.VenueExpenses.AsNoTracking().Where(e => e.StadiumId == stadiumId);
            if (from.HasValue)
            {
                var f = from.Value.Date;
                query = query.Where(e => e.Date >= f);
            }
            if (to.HasValue)
            {
                var t = to.Value.Date;
                query = query.Where(e => e.Date <= t);
            }
            return OperationResult<List<VenueExpenseDto>>.Ok(query
                .OrderByDescending(e => e.Date).ThenByDescending(e => e.ExpenseId)
                .ToList().Select(ToDto).ToList());
        }

        public OperationResult<int> SaveExpense(int me, ExpenseSaveInput input)
        {
            using var context = new MatcBuddyContext();
            VenueExpense? expense = null;
            var stadiumId = input.StadiumId;
            if (input.ExpenseId is > 0)
            {
                expense = context.VenueExpenses.FirstOrDefault(e => e.ExpenseId == input.ExpenseId.Value);
                if (expense == null) return OperationResult<int>.NotFound(ErrorCodes.Owner.ExpenseNotFound);
                // Mevcut kaydın tesisi de işletmeye ait olmalı
                var gOld = Guard(context, me, StadiumOf(context, expense.StadiumId), true);
                if (gOld != null) return Fail<int>(gOld);
                if (stadiumId <= 0) stadiumId = expense.StadiumId;
            }
            var g = Guard(context, me, StadiumOf(context, stadiumId), true);
            if (g != null) return Fail<int>(g);

            if (expense == null)
            {
                expense = new VenueExpense { CreatedAt = DateTime.Now };
                context.VenueExpenses.Add(expense);
            }
            expense.StadiumId = stadiumId;
            expense.Date = input.Date!.Value.Date;
            expense.Category = input.Category!;
            expense.Amount = input.Amount;
            expense.Description = input.Description;
            context.SaveChanges();
            return OperationResult<int>.Ok(expense.ExpenseId, Msg.Get(MessageKeys.Owner.ExpenseSaved));
        }

        public OperationResult<bool> DeleteExpense(int me, int expenseId)
        {
            using var context = new MatcBuddyContext();
            var expense = context.VenueExpenses.FirstOrDefault(e => e.ExpenseId == expenseId);
            if (expense == null) return OperationResult<bool>.NotFound(ErrorCodes.Owner.ExpenseNotFound);
            var g = Guard(context, me, StadiumOf(context, expense.StadiumId), true);
            if (g != null) return Fail<bool>(g);
            context.VenueExpenses.Remove(expense);
            context.SaveChanges();
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Owner.ExpenseDeleted));
        }

        // ------------------------------------------------------------------ Finans özeti

        /// <summary>stadiumId verilmezse işletmenin tüm tesisleri.</summary>
        public OperationResult<OwnerFinanceDto> Finance(int me, int? stadiumId, int year, int month)
        {
            using var context = new MatcBuddyContext();
            var (ids, error) = ScopeIds(context, me, stadiumId);
            if (error != null) return Fail<OwnerFinanceDto>(error);

            var monthStart = VenueFinance.MonthStart(year, month);
            var from = monthStart.AddMonths(-11); // aylık seri: son 12 ay (önceki ay da bu aralıkta)
            var to = monthStart.AddMonths(1);

            var pitches = context.Pitches.AsNoTracking().Where(p => ids!.Contains(p.StadiumId))
                .OrderBy(p => p.Name).ThenBy(p => p.PitchId).ToList();
            var pitchIds = pitches.Select(p => p.PitchId).ToList();

            var matches = context.Matchs.AsNoTracking()
                .Where(m => ids!.Contains(m.StadiumId) && m.PitchId != null && m.IsActive == MatchStatus.Approved
                            && m.MatchDate >= from && m.MatchDate < to)
                .Select(m => new { m.MatchId, PitchId = m.PitchId!.Value, m.MatchName, MatchDate = m.MatchDate!.Value, m.DurationMinutes, m.TotalPrice })
                .ToList()
                .Select(m => new VenueFinance.FinanceMatch(m.MatchId, m.PitchId, m.MatchName, m.MatchDate, m.DurationMinutes, m.TotalPrice ?? 0m))
                .ToList();

            var bookings = context.SlotBlocks.AsNoTracking()
                .Where(b => pitchIds.Contains(b.PitchId) && b.Kind == SlotBlockKind.Manual && b.StartAt >= from && b.StartAt < to)
                .ToList()
                .Select(b => new VenueFinance.FinanceBooking(b.SlotBlockId, b.PitchId, b.StartAt, b.EndAt, b.Price ?? 0m, b.IsPaid,
                    b.CustomerName, b.Reason))
                .ToList();

            var fromDate = from.Date;
            var toDate = to.Date;
            var expenses = context.VenueExpenses.AsNoTracking()
                .Where(e => ids!.Contains(e.StadiumId) && e.Date >= fromDate && e.Date < toDate)
                .ToList()
                .Select(e => new VenueFinance.FinanceExpense(e.ExpenseId, e.Date, e.Category, e.Amount, e.Description))
                .ToList();

            return OperationResult<OwnerFinanceDto>.Ok(VenueFinance.Build(year, month, DateTime.Now, matches, bookings, expenses,
                pitches.Select(p => new VenueFinance.FinancePitch(p.PitchId, p.Name)).ToList()));
        }
    }
}
