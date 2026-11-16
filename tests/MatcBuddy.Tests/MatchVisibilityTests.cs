using MatcBuddy.DataAccess.Concrete.EntityFramework;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Tests;

/// <summary>Sadece davetle açık (kapalı) maçlar: kim görür, kim katılır; akış penceresi.</summary>
public class MatchVisibilityTests
{
    [Fact]
    public void Public_match_is_open_to_everyone()
    {
        Assert.True(MatchVisibility.HasAccess(MatchVisibility.Public, false, false, false, false));
        Assert.True(MatchVisibility.CanJoin(MatchVisibility.Public, false, false, false, false, false));
    }

    [Fact]
    public void Closed_match_hidden_from_strangers()
    {
        Assert.False(MatchVisibility.HasAccess(MatchVisibility.InviteOnly, false, false, false, false));
        Assert.False(MatchVisibility.CanJoin(MatchVisibility.InviteOnly, false, false, false, false, false));
    }

    [Theory]
    [InlineData(true, false, false, false)]  // kurucu
    [InlineData(false, true, false, false)]  // davet edilen
    [InlineData(false, false, true, false)]  // kadroda kaydı var (ör. çıkıp geri dönen)
    [InlineData(false, false, false, true)]  // maç takımının üyesi
    public void Closed_match_open_to_people_with_access(bool creator, bool invited, bool participant, bool teamMember)
    {
        Assert.True(MatchVisibility.HasAccess(MatchVisibility.InviteOnly, creator, invited, participant, teamMember));
        Assert.True(MatchVisibility.CanJoin(MatchVisibility.InviteOnly, false, creator, invited, participant, teamMember));
    }

    [Fact]
    public void Invite_link_lets_anyone_join_closed_match()
    {
        Assert.True(MatchVisibility.CanJoin(MatchVisibility.InviteOnly, viaInviteLink: true, false, false, false, false));
    }

    [Theory]
    [InlineData((byte)0, true)]
    [InlineData((byte)1, true)]
    [InlineData((byte)2, false)]
    [InlineData((byte)255, false)]
    public void Only_known_visibility_values_are_valid(byte value, bool valid) =>
        Assert.Equal(valid, MatchVisibility.IsValid(value));

    [Fact]
    public void Feed_window_keeps_last_two_weeks_of_results()
    {
        var now = new DateTime(2026, 11, 16, 20, 30, 0);
        var start = EFMatchParticipationDal.FeedWindowStart(now);
        Assert.Equal(new DateTime(2026, 11, 2), start);
        Assert.True(new DateTime(2026, 11, 3, 21, 0, 0) >= start);  // 13 gün önceki sonuç akışta
        Assert.False(new DateTime(2026, 10, 30) >= start);          // daha eskisi akışta değil
    }
}
