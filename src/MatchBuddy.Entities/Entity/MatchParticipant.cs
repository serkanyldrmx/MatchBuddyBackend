using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.Entity
{
    /// <summary>Maça özel kadro kaydı (katılıyor / yedek / gelemiyor).</summary>
    public class MatchParticipant : IEntity
    {
        public int MatchParticipantId { get; set; }
        public int MatchId { get; set; }
        public Match Match { get; set; }
        public int PlayerId { get; set; }
        public Player Player { get; set; }

        // ParticipantStatus: 1 = Katılıyor, 2 = Yedek, 3 = Gelemiyor
        public byte Status { get; set; }

        // ParticipantSide: 1 = Ev sahibi, 2 = Deplasman, null = atanmadı
        public byte? Side { get; set; }

        // Maç öncesi "geliyorum" onayı
        public bool Confirmed { get; set; }

        // Yedek sırası bu alana göre belirlenir
        public DateTime JoinedAt { get; set; }

        // Hatırlatma tekrar gönderilmesin
        public DateTime? ReminderSentAt { get; set; }

        public int Goals { get; set; }
        public int Assists { get; set; }
    }

    public static class ParticipantStatus
    {
        public const byte Going = 1;
        public const byte Waitlist = 2;
        public const byte Declined = 3;
    }

    public static class ParticipantSide
    {
        public const byte Home = 1;
        public const byte Away = 2;
    }
}
