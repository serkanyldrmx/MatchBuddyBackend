using MatcBuddy.DataAccess.Concrete;

namespace MatcBuddy.Tests;

/// <summary>
/// İşletme tarafı saf kuralları: abone (haftalık seri) haftalarının dolu sayılması, bekleyen talebin düşmesi,
/// geç iptal penceresi ve finansın tahsilat / gelmedi işaretine göre sayması.
/// </summary>
public class VenueOwnerRulesTests
{
    private static readonly DateTime Next = new(2026, 11, 24, 21, 0, 0); // Salı 21:00

    [Fact]
    public void Series_occurrences_cover_every_future_week_from_next_date()
    {
        var weeks = VenueBookingRules.SeriesOccurrences(Next, 60, Next.AddDays(-1), Next.AddDays(30)).ToList();
        Assert.Equal(5, weeks.Count); // 24.11, 1.12, 8.12, 15.12, 22.12
        Assert.All(weeks, w => Assert.Equal(DayOfWeek.Tuesday, w.Start.DayOfWeek));
        Assert.All(weeks, w => Assert.Equal(TimeSpan.FromMinutes(60), w.End - w.Start));
        Assert.Equal(Next.AddDays(28), weeks[^1].Start);
    }

    [Fact]
    public void Series_occurrence_blocks_an_overlapping_booking_far_ahead()
    {
        // 9 hafta sonra 21:30–22:30 talebi, serinin 21:00–22:30 haftasıyla çakışır
        var from = Next.AddDays(63).AddMinutes(30);
        var weeks = VenueBookingRules.SeriesOccurrences(Next, 90, from, from.AddMinutes(60)).ToList();
        Assert.Single(weeks);
        Assert.Equal(Next.AddDays(63), weeks[0].Start);
    }

    [Fact]
    public void Series_occurrences_skip_other_days_and_times()
    {
        Assert.Empty(VenueBookingRules.SeriesOccurrences(Next, 60, Next.AddDays(70).AddHours(1), Next.AddDays(70).AddHours(2)));
        Assert.Empty(VenueBookingRules.SeriesOccurrences(Next, 60, Next.AddDays(3), Next.AddDays(4)));
        // Bitişik (22:00'de başlayan) dilim çakışmaz
        Assert.Empty(VenueBookingRules.SeriesOccurrences(Next, 60, Next.AddDays(7).AddHours(1), Next.AddDays(7).AddHours(2)));
        // Seri başlamadan önceki haftalar serbest
        Assert.Empty(VenueBookingRules.SeriesOccurrences(Next, 60, Next.AddDays(-7), Next.AddDays(-7).AddHours(1)));
    }

    [Fact]
    public void Pending_request_expires_after_hours_or_when_start_passes()
    {
        var now = new DateTime(2026, 11, 16, 12, 0, 0);
        var created = now.AddHours(-10);
        Assert.False(VenueBookingRules.IsPendingExpired(now.AddDays(3), created, now, 24));
        Assert.True(VenueBookingRules.IsPendingExpired(now.AddDays(3), now.AddHours(-24), now, 24));
        Assert.True(VenueBookingRules.IsPendingExpired(now.AddMinutes(-1), created, now, 24));
        Assert.True(VenueBookingRules.IsPendingExpired(now, created, now, 24));
        Assert.True(VenueBookingRules.IsPendingExpired(null, created, now, 24));
    }

    [Fact]
    public void Late_cancel_is_inside_the_free_cancellation_window_only()
    {
        var now = new DateTime(2026, 11, 16, 12, 0, 0);
        Assert.True(VenueBookingRules.IsLateCancel(now.AddHours(5), now, 24));
        Assert.False(VenueBookingRules.IsLateCancel(now.AddHours(30), now, 24));
        Assert.False(VenueBookingRules.IsLateCancel(now.AddHours(-1), now, 24)); // başlamış
        Assert.False(VenueBookingRules.IsLateCancel(now.AddHours(1), now, 0));   // ücretsiz iptal her zaman
        Assert.False(VenueBookingRules.IsLateCancel(null, now, 24));
    }

    [Fact]
    public void Finance_counts_app_matches_as_collected_only_when_paid_and_ignores_no_shows()
    {
        var now = new DateTime(2026, 11, 20, 12, 0, 0);
        var day = new DateTime(2026, 11, 10, 20, 0, 0);
        var matches = new List<VenueFinance.FinanceMatch>
        {
            new(1, 1, "Ödenen", day, 60, 1000m, IsPaid: true),
            new(2, 1, "Ödenmeyen", day.AddDays(1), 60, 800m),
            new(3, 1, "Gelmedi", day.AddDays(2), 60, 900m, IsPaid: true, NoShow: true),
            new(4, 1, "Gelecek", now.AddDays(2), 60, 700m),
        };
        var dto = VenueFinance.Build(2026, 11, now, matches, new List<VenueFinance.FinanceBooking>(),
            new List<VenueFinance.FinanceExpense>(), new List<VenueFinance.FinancePitch> { new(1, "A") });

        Assert.Equal(1000m, dto.Income.Matches);
        Assert.Equal(800m, dto.Receivable);
        Assert.Equal(700m, dto.ExpectedIncome);
        Assert.Equal(2, dto.Counts.Matches);
        Assert.DoesNotContain(dto.Transactions, t => t.RefId == 3 && t.Source == "match");
        Assert.True(dto.Transactions.Single(t => t.RefId == 1).IsPaid);
        Assert.False(dto.Transactions.Single(t => t.RefId == 2).IsPaid);
        Assert.Equal(1000m, dto.Monthly[^1].Income);
    }
}
