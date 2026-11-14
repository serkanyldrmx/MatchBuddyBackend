using MatcBuddy.Api.Services.Messaging;
using MatcBuddy.Api.Services.Push;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Tests;

/// <summary>Bildirime dokununca açılan sayfa (push ve bildirim listesi aynı kuralı kullanır).</summary>
public class NotificationUrlTests
{
    [Theory]
    [InlineData(NotificationType.MatchJoin, 12, null, "/match-details/12")]
    [InlineData(NotificationType.MatchUpdated, 12, null, "/match-details/12")]
    [InlineData(NotificationType.MatchReminder, 7, null, "/match-details/7")]
    [InlineData(NotificationType.MatchInvite, 7, null, "/invites")]
    [InlineData(NotificationType.TeamInvite, null, null, "/invites")]
    [InlineData(NotificationType.Message, null, null, "/home?messages=1")]
    [InlineData(NotificationType.NewFollower, null, null, "/friends")]
    [InlineData(NotificationType.TournamentUpdate, null, null, "/tournaments")]
    [InlineData(NotificationType.ReservationRequest, 5, null, "/venue/reservations")]
    [InlineData(NotificationType.VenueReview, 5, null, "/venue/reviews")]
    [InlineData(NotificationType.SlotDeal, null, 3, "/venues/3")]
    [InlineData(NotificationType.StadiumAdded, null, 3, "/venues/3")]
    [InlineData(NotificationType.MatchRejection, null, 3, "/NotificationList")] // silinmiş maç: gidilecek maç yok
    public void UrlFor_maps_every_type(NotificationType type, int? matchId, int? stadiumId, string expected) =>
        Assert.Equal(expected, PushDispatchService.UrlFor(type, matchId, stadiumId));
}

/// <summary>Kod gönderim kanalları: canlıda gerçek gönderici yoksa kanal kapalıdır (kullanıcıya boşuna "gönderildi" denmez).</summary>
public class MessagingChannelsTests
{
    [Fact]
    public void Production_without_senders_has_no_channels()
    {
        var c = new MessagingChannels(emailConfigured: false, smsConfigured: false, isDevelopment: false);
        Assert.False(c.IsAvailable("email"));
        Assert.False(c.IsAvailable("sms"));
    }

    [Fact]
    public void Development_outbox_counts_as_available()
    {
        var c = new MessagingChannels(false, false, isDevelopment: true);
        Assert.True(c.IsAvailable("email"));
        Assert.True(c.IsAvailable("SMS"));
    }

    [Fact]
    public void Configured_sender_is_available_in_production()
    {
        var c = new MessagingChannels(emailConfigured: true, smsConfigured: false, isDevelopment: false);
        Assert.True(c.IsAvailable("email"));
        Assert.False(c.IsAvailable("sms"));
    }
}
