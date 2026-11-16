using System.Reflection;
using MatcBuddy.Business.Auth;
using MatcBuddy.Business.Concrete;
using MatcBuddy.Core.Errors;
using MatcBuddy.Core.Messaging;
using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;
using Microsoft.Extensions.Logging.Abstractions;

namespace MatcBuddy.Tests;

/// <summary>Kullanıcı engeli: engel süresi, giriş / yenileme reddi, yönetici kuralları, şikayetten engelleme.</summary>
public class ModerationTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

    private sealed class FakeDal : IModerationDal
    {
        public Dictionary<int, Player> Players = new();
        public Dictionary<int, Report> Reports = new();
        public PagedDto<AdminUserDto> SearchUsers(string? query, bool bannedOnly, int page, int pageSize, DateTime utcNow) =>
            new() { Page = page, PageSize = pageSize };
        public Player? FindPlayer(int playerId) => Players.TryGetValue(playerId, out var p) && !p.IsDeleted ? p : null;
        public void SetBan(int playerId, bool banned, DateTime? untilUtc, string? reason)
        {
            var p = Players[playerId];
            p.IsBanned = banned;
            p.BannedUntil = banned ? untilUtc : null;
            p.BanReason = banned ? reason : null;
        }
        public Report? FindReport(int reportId) => Reports.GetValueOrDefault(reportId);
        public void ResolveReport(int reportId, int adminId, string? note, byte action, DateTime utcNow)
        {
            var r = Reports[reportId];
            r.Status = ReportStatus.Reviewed;
            r.ResolvedByPlayerId = adminId;
            r.ResolvedAt = utcNow;
            r.ResolutionNote = note;
            r.ResolutionAction = action;
        }
    }

    private static (ModerationManager Manager, FakeDal Dal) Setup()
    {
        var dal = new FakeDal();
        dal.Players[1] = new Player { PlayerId = 1, Role = PlayerRoles.Admin, IsAdmin = 1 };
        dal.Players[2] = new Player { PlayerId = 2, Role = PlayerRoles.Player };
        dal.Players[3] = new Player { PlayerId = 3, Role = PlayerRoles.Admin, IsAdmin = 1 };
        dal.Reports[10] = new Report { ReportId = 10, ReporterId = 3, ReportedPlayerId = 2, Reason = "hakaret" };
        return (new ModerationManager(dal, () => Now), dal);
    }

    // ------------------------------------------------------------------ Player.IsBanActive / AuthManager.BanError

    [Fact]
    public void Ban_is_active_when_permanent_or_until_in_future()
    {
        Assert.False(new Player().IsBanActive(Now));
        Assert.True(new Player { IsBanned = true }.IsBanActive(Now));
        Assert.True(new Player { IsBanned = true, BannedUntil = Now.AddMinutes(1) }.IsBanActive(Now));
        Assert.False(new Player { IsBanned = true, BannedUntil = Now }.IsBanActive(Now));
        Assert.False(new Player { IsBanned = true, BannedUntil = Now.AddDays(-1) }.IsBanActive(Now));
        // Bayrak kapalıyken tarih tek başına engel değildir
        Assert.False(new Player { BannedUntil = Now.AddDays(1) }.IsBanActive(Now));
    }

    [Fact]
    public void BanError_maps_to_banned_codes()
    {
        Assert.Null(AuthManager.BanError(new Player(), Now));
        Assert.Null(AuthManager.BanError(new Player { IsBanned = true, BannedUntil = Now.AddHours(-1) }, Now));

        var permanent = AuthManager.BanError(new Player { IsBanned = true }, Now);
        Assert.Equal(ErrorCodes.Auth.AccountBanned, permanent!.Code);
        Assert.Equal(OperationStatus.Forbidden, permanent.Status);

        var until = AuthManager.BanError(new Player { IsBanned = true, BannedUntil = Now.AddDays(3) }, Now);
        Assert.Equal(ErrorCodes.Auth.AccountBannedUntil, until!.Code);
        Assert.Single(until.Args);
        Assert.Contains("2026", until.Args[0]!.ToString());
    }

    // ------------------------------------------------------------------ Ban / Unban

    [Fact]
    public void Admin_cannot_ban_self_or_another_admin()
    {
        var (m, dal) = Setup();
        Assert.Equal(ErrorCodes.Moderation.CannotBanSelf, m.Ban(1, new BanUserInput { PlayerId = 1 }).Code);
        Assert.Equal(ErrorCodes.Moderation.CannotBanAdmin, m.Ban(1, new BanUserInput { PlayerId = 3 }).Code);
        Assert.False(dal.Players[3].IsBanned);
    }

    [Fact]
    public void Ban_validates_until_reason_and_player()
    {
        var (m, _) = Setup();
        Assert.Equal(ErrorCodes.Moderation.BanUntilPast, m.Ban(1, new BanUserInput { PlayerId = 2, Until = Now.AddMinutes(-1) }).Code);
        Assert.Equal(ErrorCodes.Validation.TooLong, m.Ban(1, new BanUserInput { PlayerId = 2, Reason = new string('x', 501) }).Code);
        Assert.Equal(ErrorCodes.Player.NotFound, m.Ban(1, new BanUserInput { PlayerId = 99 }).Code);
    }

    [Fact]
    public void Ban_and_unban_update_the_player()
    {
        var (m, dal) = Setup();
        var until = Now.AddDays(7);
        var r = m.Ban(1, new BanUserInput { PlayerId = 2, Until = until, Reason = "  küfür  " });
        Assert.True(r.Success);
        Assert.True(dal.Players[2].IsBanActive(Now));
        Assert.Equal(until, dal.Players[2].BannedUntil);
        Assert.Equal("küfür", dal.Players[2].BanReason);

        Assert.True(m.Unban(2).Success);
        Assert.False(dal.Players[2].IsBanned);
        Assert.Null(dal.Players[2].BannedUntil);
        // Engelli olmayanın engelini kaldırmak da başarılıdır
        Assert.True(m.Unban(2).Success);
    }

    [Fact]
    public void Unspecified_until_is_treated_as_utc()
    {
        var local = new DateTime(2026, 12, 1, 10, 0, 0, DateTimeKind.Unspecified);
        Assert.Equal(DateTimeKind.Utc, ModerationManager.ToUtc(local).Kind);
        Assert.Equal(10, ModerationManager.ToUtc(local).Hour);
    }

    // ------------------------------------------------------------------ Şikayet sonuçlandırma

    [Fact]
    public void Resolve_without_action_records_note_and_admin()
    {
        var (m, dal) = Setup();
        var r = m.ResolveReport(1, new ResolveReportInput { ReportId = 10, Note = "uyarıldı", Action = ReportResolutionActions.Warn });
        Assert.True(r.Success);
        Assert.Null(r.Data);
        var report = dal.Reports[10];
        Assert.Equal(ReportStatus.Reviewed, report.Status);
        Assert.Equal(1, report.ResolvedByPlayerId);
        Assert.Equal(Now, report.ResolvedAt);
        Assert.Equal("uyarıldı", report.ResolutionNote);
        Assert.Equal(ReportResolutionActions.Warn, report.ResolutionAction);
        Assert.False(dal.Players[2].IsBanned);
    }

    [Fact]
    public void Resolve_with_ban_bans_the_reported_player()
    {
        var (m, dal) = Setup();
        var r = m.ResolveReport(1, new ResolveReportInput { ReportId = 10, Note = "tekrarlayan hakaret", Action = ReportResolutionActions.Ban });
        Assert.True(r.Success);
        Assert.Equal(2, r.Data);
        Assert.True(dal.Players[2].IsBanActive(Now));
        Assert.Equal("tekrarlayan hakaret", dal.Players[2].BanReason);
        Assert.Equal(ReportResolutionActions.Ban, dal.Reports[10].ResolutionAction);
    }

    [Fact]
    public void Resolve_rejects_unknown_action_and_missing_report()
    {
        var (m, dal) = Setup();
        Assert.Equal(ErrorCodes.Validation.InvalidValue, m.ResolveReport(1, new ResolveReportInput { ReportId = 10, Action = 9 }).Code);
        Assert.Equal(ErrorCodes.Social.ReportNotFound, m.ResolveReport(1, new ResolveReportInput { ReportId = 77 }).Code);
        Assert.Equal(ReportStatus.New, dal.Reports[10].Status);
    }

    [Fact]
    public void Failed_ban_leaves_report_open()
    {
        var (m, dal) = Setup();
        dal.Reports[11] = new Report { ReportId = 11, ReporterId = 2, ReportedPlayerId = 3, Reason = "spam" };
        var r = m.ResolveReport(1, new ResolveReportInput { ReportId = 11, Action = ReportResolutionActions.Ban });
        Assert.Equal(ErrorCodes.Moderation.CannotBanAdmin, r.Code);
        Assert.Equal(ReportStatus.New, dal.Reports[11].Status);
    }

    // ------------------------------------------------------------------ Oturum yenileme

    /// <summary>IAuthDal'ın yalnızca Refresh'in kullandığı üyeleri; diğerleri çağrılırsa test kırılır.</summary>
    public class AuthDalProxy : DispatchProxy
    {
        public RefreshToken? Token;
        public Player? Player;
        public List<(int PlayerId, string Reason)> RevokedAll = new();

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method!.Name)
            {
                case nameof(IAuthDal.FindRefreshToken): return Token;
                case nameof(IAuthDal.FindPlayer): return Player;
                case nameof(IAuthDal.RevokeAllForPlayer):
                    RevokedAll.Add(((int)args![0]!, (string)args[1]!));
                    return 1;
                default: throw new InvalidOperationException($"Beklenmeyen çağrı: {method.Name}");
            }
        }
    }

    private sealed class NoQueue : IOutgoingMessageQueue
    {
        public void Enqueue(EmailMessage message) { }
        public void Enqueue(SmsMessage message) { }
    }

    [Fact]
    public void Refresh_for_banned_player_revokes_all_sessions()
    {
        var dal = DispatchProxy.Create<IAuthDal, AuthDalProxy>();
        var proxy = (AuthDalProxy)(object)dal;
        proxy.Player = new Player { PlayerId = 2, IsBanned = true };
        proxy.Token = new RefreshToken { RefreshTokenId = 5, PlayerId = 2, FamilyId = Guid.NewGuid(), ExpiresAt = DateTime.UtcNow.AddDays(1) };
        var auth = new AuthManager(dal, new NoQueue(), new AuthSettings(), NullLogger<AuthManager>.Instance);

        var result = auth.Refresh("raw-token", new ClientInfo(null, null));

        Assert.False(result.Success);
        Assert.Equal(OperationStatus.Unauthorized, result.Status);
        Assert.Equal(ErrorCodes.Auth.AccountBanned, result.Code);
        Assert.Equal((2, RevokeReasons.Banned), Assert.Single(proxy.RevokedAll));
    }
}
