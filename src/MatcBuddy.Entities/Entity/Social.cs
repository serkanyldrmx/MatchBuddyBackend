using MatcBuddy.Core.Entities;

namespace MatcBuddy.Entities.Entity
{
    /// <summary>Tek yönlü takip: Follower, Followee'yi takip eder.</summary>
    public class Follow : IEntity
    {
        public int FollowId { get; set; }
        public int FollowerId { get; set; }
        public Player Follower { get; set; }
        public int FolloweeId { get; set; }
        public Player Followee { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>Engel: Blocker, Blocked'ı engeller. Kontroller her zaman iki yönlüdür.</summary>
    public class Block : IEntity
    {
        public int BlockId { get; set; }
        public int BlockerId { get; set; }
        public Player Blocker { get; set; }
        public int BlockedId { get; set; }
        public Player Blocked { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>Oyuncu şikayeti. Status: 0 yeni, 1 incelendi.</summary>
    public class Report : IEntity
    {
        public int ReportId { get; set; }
        public int ReporterId { get; set; }
        public Player Reporter { get; set; }
        public int ReportedPlayerId { get; set; }
        public Player ReportedPlayer { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? Details { get; set; }
        public byte Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public static class ReportStatus
    {
        public const byte New = 0;
        public const byte Reviewed = 1;
    }

    /// <summary>Oyuncu ayarları (1:1). Kayıt yoksa varsayılanlar geçerlidir.</summary>
    public class PlayerSettings : IEntity
    {
        public int PlayerId { get; set; }
        public Player Player { get; set; }

        // 0 herkes, 1 sadece arkadaşlar (karşılıklı takip), 2 arkadaşlar + birlikte oynadıklarım (varsayılan)
        public byte MessagePermission { get; set; } = MessagePermissions.FriendsAndPlayedWith;

        // 0 herkes (varsayılan), 1 takipçilerim, 2 sadece ben
        public byte ProfileVisibility { get; set; } = ProfileVisibilities.Everyone;

        public bool NotifyFollow { get; set; } = true;
        public bool NotifyMatchInvite { get; set; } = true;
        public bool NotifyTeamInvite { get; set; } = true;
        public bool NotifyMatchReminder { get; set; } = true;
        public bool NotifyMatchResult { get; set; } = true;
        public bool NotifyMatchUpdates { get; set; } = true;
        // İşletmelerin "boş saat / indirim" bildirimleri
        public bool NotifyDeals { get; set; } = true;

        // Arayüz tercihleri (docs/platform-api.md): "system" | "light" | "dark"
        public string Theme { get; set; } = PlayerThemes.System;

        // Arayüz dili (BCP-47 kısa kod). Varsayılan "tr"; istemci Accept-Language başlığında gönderir.
        public string Language { get; set; } = "tr";

        public static PlayerSettings Defaults(int playerId) => new() { PlayerId = playerId };
    }

    public static class MessagePermissions
    {
        public const byte Everyone = 0;
        public const byte FriendsOnly = 1;
        public const byte FriendsAndPlayedWith = 2;
    }

    public static class ProfileVisibilities
    {
        public const byte Everyone = 0;
        public const byte Followers = 1;
        public const byte OnlyMe = 2;
    }

    /// <summary>Maç daveti. Status: 0 bekliyor, 1 kabul, 2 red.</summary>
    public class MatchInvite : IEntity
    {
        public int InviteId { get; set; }
        public int MatchId { get; set; }
        public Match Match { get; set; }
        public int InviterId { get; set; }
        public Player Inviter { get; set; }
        public int InviteeId { get; set; }
        public Player Invitee { get; set; }
        public byte Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? RespondedAt { get; set; }

        // Takım daveti: kabul edilince oyuncu bu tarafa yerleşir (taraf doluysa normal Join kuralı)
        public byte? Side { get; set; }
    }

    /// <summary>Takım daveti. Status: 0 bekliyor, 1 kabul, 2 red.</summary>
    public class TeamInvite : IEntity
    {
        public int InviteId { get; set; }
        public int TeamId { get; set; }
        public Team Team { get; set; }
        public int InviterId { get; set; }
        public Player Inviter { get; set; }
        public int InviteeId { get; set; }
        public Player Invitee { get; set; }
        public byte Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? RespondedAt { get; set; }
    }

    public static class InviteStatus
    {
        public const byte Pending = 0;
        public const byte Accepted = 1;
        public const byte Declined = 2;
    }

    /// <summary>Akış (feed) aktivitesi.</summary>
    public class Activity : IEntity
    {
        public int ActivityId { get; set; }
        public int ActorPlayerId { get; set; }
        public Player ActorPlayer { get; set; }
        public byte Type { get; set; }
        public int? MatchId { get; set; }
        public Match? Match { get; set; }
        public int? TeamId { get; set; }
        public Team? Team { get; set; }
        public int? TargetPlayerId { get; set; }
        public Player? TargetPlayer { get; set; }
        public int? Value { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public static class ActivityType
    {
        public const byte MatchCreated = 1;
        public const byte JoinedMatch = 2;
        public const byte Scored = 3;
        public const byte MvpWon = 4;
        public const byte TeamCreated = 5;
        public const byte Followed = 6;
        public const byte TournamentWon = 7; // turnuva paketi kullanacak
    }
}
