using MatcBuddy.Core.Entities;

namespace MatcBuddy.Entities.Entity
{
    public class GroupPlayer : IEntity
    {
        public int GroupPlayerId { get; set; }
        public int GroupId { get; set; }
        public int PlayerId { get; set; }
        // Üyenin grup sohbetini en son açtığı an (okunmamış sayısı için; null = hiç açmadı)
        public DateTime? LastReadAt { get; set; }
        public Group Group { get; set; }
        public Player Player { get; set; }
    }
}
