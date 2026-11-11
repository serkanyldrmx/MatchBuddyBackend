using MatchBuddy.Core.Entities;
using MatchBuddy.Core.Parameters;

namespace MatchBuddy.Entities.Entity
{
    /// <summary>Yönetici tarafından değiştirilebilen sistem parametresi (docs/platform-api.md).</summary>
    public class SystemParameter : IEntity
    {
        public int SystemParameterId { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public ParameterDataType DataType { get; set; }
        public string Group { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? MinValue { get; set; }
        public string? MaxValue { get; set; }
        public bool IsPublic { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int? UpdatedByPlayerId { get; set; }
    }

    /// <summary>Parametre değişiklik geçmişi.</summary>
    public class ParameterAudit : IEntity
    {
        public long ParameterAuditId { get; set; }
        public int SystemParameterId { get; set; }
        public string Key { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string NewValue { get; set; } = string.Empty;
        public int? ChangedByPlayerId { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    /// <summary>
    /// Tesis (stadyum) düzeyinde işletme ayarları. null alan = sistem varsayılanı (booking.* parametreleri).
    /// </summary>
    public class VenueSettings : IEntity
    {
        public int StadiumId { get; set; }
        public int? MinBookingNoticeHours { get; set; }
        public int? FreeCancellationHours { get; set; }
        public int? MaxAdvanceBookingDays { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int? UpdatedByPlayerId { get; set; }
    }

    public static class PlayerThemes
    {
        public const string System = "system";
        public const string Light = "light";
        public const string Dark = "dark";
        public static readonly string[] All = { System, Light, Dark };
    }
}
