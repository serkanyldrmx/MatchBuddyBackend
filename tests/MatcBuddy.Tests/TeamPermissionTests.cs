using System.Linq.Expressions;
using MatcBuddy.Business.Concrete;
using MatcBuddy.Core.Errors;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;

namespace MatcBuddy.Tests;

/// <summary>Takım yönetimi: silme / kadro düzenleme kurucuya aittir; eski (kurucusu bilinmeyen) takımlarda her üye.</summary>
public class TeamPermissionTests
{
    private sealed class FakeTeamDal : ITeamDal
    {
        public int? Creator;
        public HashSet<int> Members = new();
        public bool LeaveResult = true;

        public int? GetCreatorId(int teamId) => Creator;
        public bool IsPlayerInTeam(int teamId, int playerId) => Members.Contains(playerId);
        public bool Leave(int teamId, int playerId) => LeaveResult;

        public List<GetTeamAndPlayer> GetTeamAndPlayer() => throw new NotSupportedException();
        public void SaveTeamByPlayerId(List<int> playerIds, int teamId) => throw new NotSupportedException();
        public bool IsInAnyTournament(int teamId) => false;
        public void AddWithActivity(Team team, int creatorPlayerId) => throw new NotSupportedException();
        public void ClearMatchReferences(int teamId) { }
        public List<Team> GetAll(Expression<Func<Team, bool>> filter = null!) => throw new NotSupportedException();
        public Team Get(Expression<Func<Team, bool>> filter) => throw new NotSupportedException();
        public void Add(Team entity) => throw new NotSupportedException();
        public void Update(Team entity) => throw new NotSupportedException();
        public void Delete(Team entity) => throw new NotSupportedException();
    }

    [Fact]
    public void Only_creator_manages_team_with_known_creator()
    {
        var dal = new FakeTeamDal { Creator = 1, Members = { 1, 2 } };
        var m = new TeamManager(dal);
        Assert.True(m.CanManage(10, 1));
        Assert.False(m.CanManage(10, 2)); // üye ama kurucu değil
        Assert.False(m.CanManage(10, 3));
    }

    [Fact]
    public void Legacy_team_without_creator_is_managed_by_members()
    {
        var dal = new FakeTeamDal { Creator = null, Members = { 2 } };
        var m = new TeamManager(dal);
        Assert.True(m.CanManage(10, 2));
        Assert.False(m.CanManage(10, 3));
    }

    [Fact]
    public void Leave_requires_membership_and_another_member()
    {
        var dal = new FakeTeamDal { Members = { 1 } };
        var m = new TeamManager(dal);
        Assert.Equal(ErrorCodes.Team.NotMember, m.Leave(10, 5).Code);

        dal.LeaveResult = false;
        Assert.Equal(ErrorCodes.Team.LastMember, m.Leave(10, 1).Code);

        dal.LeaveResult = true;
        Assert.True(m.Leave(10, 1).Success);
    }
}
