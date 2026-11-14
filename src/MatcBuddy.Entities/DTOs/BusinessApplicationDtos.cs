using MatcBuddy.Core.Entities;

namespace MatcBuddy.Entities.DTOs
{
    /// <summary>POST /api/BusinessApplications gövdesi (herkese açık).</summary>
    public class BusinessApplicationInput : IDto
    {
        public string? BusinessName { get; set; }
        public string? ContactName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public int? PitchCount { get; set; }
        public string? Note { get; set; }
        /// <summary>KVKK aydınlatma metni onayı (zorunlu)</summary>
        public bool Consent { get; set; }
    }

    public class BusinessApplicationDto : IDto
    {
        public int BusinessApplicationId { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public int? PitchCount { get; set; }
        public string? Note { get; set; }
        public byte Status { get; set; }
        public string? AdminNote { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>Yönetici: durum / not güncelleme</summary>
    public class BusinessApplicationUpdateInput : IDto
    {
        public int BusinessApplicationId { get; set; }
        public int Status { get; set; }
        public string? AdminNote { get; set; }
    }
}
