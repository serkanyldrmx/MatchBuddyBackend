using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.DTOs
{
    /// <summary>GET /api/Admin/Parameters satırı (docs/platform-api.md).</summary>
    public class ParameterDto : IDto
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        /// <summary>"int" | "decimal" | "bool" | "string" | "json"</summary>
        public string DataType { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? MinValue { get; set; }
        public string? MaxValue { get; set; }
        public string DefaultValue { get; set; } = string.Empty;
        public bool IsPublic { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int? UpdatedByPlayerId { get; set; }
        public string? UpdatedByUserName { get; set; }
    }

    public class ParameterGroupDto : IDto
    {
        public string Group { get; set; } = string.Empty;
        public List<ParameterDto> Parameters { get; set; } = new();
    }

    public class ParameterAuditDto : IDto
    {
        public long Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string NewValue { get; set; } = string.Empty;
        public int? ChangedByPlayerId { get; set; }
        public string? ChangedByUserName { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    public class UpdateParameterInput : IDto
    {
        /// <summary>Yeni değer, metin olarak (ör. "15", "true", "[60,90]").</summary>
        public string? Value { get; set; }
    }

    /// <summary>GET/PUT /api/Owner/Settings (docs/platform-api.md).</summary>
    public class VenueSettingsDto : IDto
    {
        public int StadiumId { get; set; }
        /// <summary>Tesise özel değer (null = sistem varsayılanı kullanılıyor).</summary>
        public int? MinBookingNoticeHours { get; set; }
        public int? FreeCancellationHours { get; set; }
        public int? MaxAdvanceBookingDays { get; set; }
        /// <summary>Geçerli (uygulanan) değerler: tesis değeri ya da sistem varsayılanı.</summary>
        public VenueSettingsValuesDto Effective { get; set; } = new();
        public VenueSettingsValuesDto Defaults { get; set; } = new();
        public DateTime? UpdatedAt { get; set; }
    }

    public class VenueSettingsValuesDto : IDto
    {
        public int MinBookingNoticeHours { get; set; }
        public int FreeCancellationHours { get; set; }
        public int MaxAdvanceBookingDays { get; set; }
    }

    public class VenueSettingsInput : IDto
    {
        public int StadiumId { get; set; }
        /// <summary>null gönderilen alan sistem varsayılanına döner.</summary>
        public int? MinBookingNoticeHours { get; set; }
        public int? FreeCancellationHours { get; set; }
        public int? MaxAdvanceBookingDays { get; set; }
    }
}
