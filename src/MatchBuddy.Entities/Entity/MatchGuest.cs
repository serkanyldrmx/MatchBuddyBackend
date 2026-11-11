using MatchBuddy.Core.Entities;

namespace MatchBuddy.Entities.Entity
{
    /// <summary>
    /// Uygulamada hesabı olmayan, maçı yöneten kişinin isimle kadroya eklediği misafir oyuncu
    /// (docs/match-participation-api.md "Ek: Misafir oyuncular"). Kapasiteye ve taraf sayısına dahildir;
    /// MVP / puanlama / istatistik / sıralamaya dahil değildir.
    /// </summary>
    public class MatchGuest : IEntity
    {
        public int GuestId { get; set; }
        public int MatchId { get; set; }
        public Match Match { get; set; }

        // 2–60 karakter (kırpılmış)
        public string Name { get; set; } = string.Empty;

        // En fazla 200 karakter, boşsa null
        public string? Note { get; set; }

        // ParticipantSide: 1 = Ev sahibi, 2 = Deplasman
        public byte Side { get; set; }

        public int AddedByPlayerId { get; set; }
        public Player AddedByPlayer { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public static class MatchGuestRules
    {
        public const int NameMinLength = 2;
        public const int NameMaxLength = 60;
        public const int NoteMaxLength = 200;
    }
}
