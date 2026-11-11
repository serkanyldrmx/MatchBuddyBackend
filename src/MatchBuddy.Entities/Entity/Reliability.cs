using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.Entity
{
    /// <summary>
    /// Güvenilirlik puanı hareketi (docs/reliability-api.md). Puan olaylardan hesaplanır:
    /// 100'den başlanır, geri alınmamış olaylar sırayla uygulanır, her adımda 0–100 arasında tutulur.
    /// Players.ReliabilityScore yalnızca önbellektir.
    /// </summary>
    public class ReliabilityEvent : IEntity
    {
        public int ReliabilityEventId { get; set; }
        public int PlayerId { get; set; }
        public Player Player { get; set; } = null!;
        // Bilinçli olarak yabancı anahtar yok: maç silinse de puan geçmişi kalır
        public int? MatchId { get; set; }

        /// <summary>ReliabilityEventType</summary>
        public byte Type { get; set; }
        public int Delta { get; set; }

        /// <summary>Oyuncunun seçtiği mazeret (ExcuseReason) — yalnızca iptallerde.</summary>
        public string? ReasonCode { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>Kurucu mazereti kabul etti → olay geri alındı, puan iade.</summary>
        public DateTime? ReversedAt { get; set; }
        public int? ReversedByPlayerId { get; set; }
    }

    public static class ReliabilityEventType
    {
        public const byte Attended = 1;
        public const byte LateCancel = 2;
        public const byte VeryLateCancel = 3;
        public const byte NoShow = 4;
        public const byte OrganizerLateCancel = 5;

        public static string Name(byte type) => type switch
        {
            Attended => "attended",
            LateCancel => "lateCancel",
            VeryLateCancel => "veryLateCancel",
            NoShow => "noShow",
            OrganizerLateCancel => "organizerLateCancel",
            _ => "unknown",
        };

        public static bool IsPenalty(byte type) => type != Attended;
    }

    /// <summary>Hızlı iptal penceresindeki mazeret seçenekleri.</summary>
    public static class ExcuseReason
    {
        public const string Emergency = "emergency";
        public const string Health = "health";
        public const string Bereavement = "bereavement";
        public const string Work = "work";
        public const string Transport = "transport";
        public const string Other = "other";

        public static readonly IReadOnlyList<string> All = new[] { Emergency, Health, Bereavement, Work, Transport, Other };
        public const int NoteMaxLength = 200;
    }

    /// <summary>Gösterim seviyesi (rozet rengi).</summary>
    public static class ReliabilityLevel
    {
        public const string New = "new";
        public const string Good = "good";
        public const string Medium = "medium";
        public const string Risky = "risky";
    }
}
