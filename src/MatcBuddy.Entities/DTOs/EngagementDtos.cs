namespace MatcBuddy.Entities.DTOs
{
    // ================================================================== Mesajlaşma (docs/engagement-api.md §1)

    /// <summary>Konuşmadaki mesaj (birebir: MessageId, grup: GroupMessageId). Silinmiş mesajda metin / resim boştur.</summary>
    public class ChatMessageDto
    {
        public int? MessageId { get; set; }
        public int? GroupMessageId { get; set; }
        public int? GroupId { get; set; }
        public string? MatchMessage { get; set; }
        public DateTime SendingDate { get; set; }
        public int? SendPlayerId { get; set; }
        public int? RecipientPlayerId { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime? ReadAt { get; set; }
        public byte Status { get; set; }
        public bool IsDeleted { get; set; }
        // Gönderen (grup sohbetinde gösterilir)
        public string? UserName { get; set; }
        public string? PlayerName { get; set; }
        public string? PlayerSurname { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public ReplyPreviewDto? ReplyTo { get; set; }
        public List<ReactionSummaryDto> Reactions { get; set; } = new();
    }

    public class ReplyPreviewDto
    {
        public int Id { get; set; }
        public int? SendPlayerId { get; set; }
        public string? SenderName { get; set; }
        public string? Text { get; set; }
        public bool HasImage { get; set; }
        public bool IsDeleted { get; set; }
    }

    public class ReactionSummaryDto
    {
        public string Emoji { get; set; } = string.Empty;
        public int Count { get; set; }
        public bool Mine { get; set; }
        /// <summary>Tepki verenlerin adları (en fazla 10)</summary>
        public List<string> Names { get; set; } = new();
    }

    /// <summary>Tepki: emoji boş/null = tepkimi kaldır; aynı emoji tekrar = kaldır.</summary>
    public class ReactInput
    {
        public int? MessageId { get; set; }
        public int? GroupMessageId { get; set; }
        public string? Emoji { get; set; }
    }

    /// <summary>Tepki / silme sonrası anlık sinyal için konuşmanın karşı tarafı ya da grubu.</summary>
    public class ChatTargetDto
    {
        public bool IsGroup { get; set; }
        public int GroupId { get; set; }
        public int SenderId { get; set; }
        public int RecipientId { get; set; }
    }

    // ================================================================== Hava durumu (§2)

    public class MatchWeatherDto
    {
        public int MatchId { get; set; }
        /// <summary>Tahmin var mı (maç 16 günden yakın, konum biliniyor, kapalı saha değil)</summary>
        public bool Available { get; set; }
        /// <summary>Kapalı saha: hava durumu gösterilmez</summary>
        public bool Indoor { get; set; }
        public DateTime? Time { get; set; }
        public double? Temperature { get; set; }
        public double? ApparentTemperature { get; set; }
        public int? PrecipitationProbability { get; set; }
        public double? Precipitation { get; set; }
        public int? WeatherCode { get; set; }
        public double? WindSpeed { get; set; }
        /// <summary>Konum ilçe/şehirden tahmin edildi (sahanın tam konumu yok)</summary>
        public bool Approximate { get; set; }
    }

    // ================================================================== Davet linki (§3)

    public class ShareLinkDto
    {
        public string Code { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        /// <summary>WhatsApp'a hazır metin (link dahil)</summary>
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>Herkese açık davet sayfası / önizleme kartı (giriş gerekmez).</summary>
    public class PublicMatchInviteDto
    {
        public string Code { get; set; } = string.Empty;
        public int MatchId { get; set; }
        public string? MatchName { get; set; }
        public DateTime? MatchDate { get; set; }
        public int DurationMinutes { get; set; }
        public string? VenueName { get; set; }
        public string? District { get; set; }
        public string? City { get; set; }
        public bool IsExternal { get; set; }
        public int Capacity { get; set; }
        public int GoingCount { get; set; }
        public int Missing { get; set; }
        public decimal? PerPlayer { get; set; }
        public string? HostName { get; set; }
        public string? HostPhotoUrl { get; set; }
        public List<PlayerSummaryDto> Participants { get; set; } = new();
        public string? HomeTeam { get; set; }
        public string? AwayTeam { get; set; }
        public bool IsPast { get; set; }
        /// <summary>Reddedildi / iptal: katılınamaz</summary>
        public bool IsClosed { get; set; }
    }

    // ================================================================== Harici saha satış listesi (§4)

    /// <summary>Aynı ad + şehirdeki harici sahalar tek satır: admin bu işletmeyi arar.</summary>
    public class ExternalVenueLeadDto
    {
        public string Key { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public List<string> Phones { get; set; } = new();
        public string? Address { get; set; }
        public string? Location { get; set; }
        public List<int> StadiumIds { get; set; } = new();
        /// <summary>Bu sahayı ekleyen farklı oyuncu sayısı</summary>
        public int AddedBy { get; set; }
        public int MatchCount { get; set; }
        public int UpcomingCount { get; set; }
        /// <summary>Bu sahada oynamış farklı oyuncu sayısı</summary>
        public int PlayerCount { get; set; }
        public DateTime? FirstMatchDate { get; set; }
        public DateTime? LastMatchDate { get; set; }
    }

    public class LinkTargetDto
    {
        public int StadiumId { get; set; }
        public string? Name { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public bool HasOwner { get; set; }
    }

    /// <summary>
    /// Harici sahaları bağla: TargetStadiumId (mevcut tesis) ya da CreateCandidate (yeni aday tesis; işletmesi
    /// olmadığı için oyunculara görünmez, admin sonra işletme hesabı verir). Maçlar hedef tesise taşınır.
    /// </summary>
    public class LinkExternalInput
    {
        public List<int> StadiumIds { get; set; } = new();
        public int? TargetStadiumId { get; set; }
        public bool CreateCandidate { get; set; }
        public string? Name { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Location { get; set; }
    }
}
