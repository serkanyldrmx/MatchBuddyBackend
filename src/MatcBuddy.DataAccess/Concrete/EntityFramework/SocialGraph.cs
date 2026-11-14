using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatcBuddy.DataAccess.Concrete.EntityFramework
{
    /// <summary>Bir oyuncunun ("ben") takip / takipçi / engel ilişkileri ve birlikte oynama sayıları.</summary>
    public class SocialGraph
    {
        public int Me { get; private init; }
        public HashSet<int> Following { get; private init; } = new();   // benim takip ettiklerim
        public HashSet<int> Followers { get; private init; } = new();   // beni takip edenler
        public HashSet<int> BlockedByMe { get; private init; } = new();
        public HashSet<int> BlockedMe { get; private init; } = new();
        public HashSet<int> BlockedEither { get; private init; } = new(); // iki yönlü engel

        private Dictionary<int, int>? _playedTogether;
        private readonly MatcBuddyContext _context;

        private SocialGraph(MatcBuddyContext context) { _context = context; }

        public static SocialGraph Load(MatcBuddyContext context, int me)
        {
            var following = context.Follows.AsNoTracking().Where(f => f.FollowerId == me).Select(f => f.FolloweeId).ToHashSet();
            var followers = context.Follows.AsNoTracking().Where(f => f.FolloweeId == me).Select(f => f.FollowerId).ToHashSet();
            var blocks = context.Blocks.AsNoTracking()
                .Where(b => b.BlockerId == me || b.BlockedId == me)
                .Select(b => new { b.BlockerId, b.BlockedId })
                .ToList();
            var byMe = blocks.Where(b => b.BlockerId == me).Select(b => b.BlockedId).ToHashSet();
            var me2 = blocks.Where(b => b.BlockedId == me).Select(b => b.BlockerId).ToHashSet();
            return new SocialGraph(context)
            {
                Me = me,
                Following = following,
                Followers = followers,
                BlockedByMe = byMe,
                BlockedMe = me2,
                BlockedEither = byMe.Union(me2).ToHashSet(),
            };
        }

        public bool IFollow(int id) => Following.Contains(id);
        public bool FollowsMe(int id) => Followers.Contains(id);
        public bool IsMutual(int id) => Following.Contains(id) && Followers.Contains(id);
        public bool IsBlocked(int id) => BlockedEither.Contains(id);

        /// <summary>Birlikte oynanan maç sayısı: ikisinin de aynı maçta Status = Going kaydı (tarih fark etmez).</summary>
        public Dictionary<int, int> PlayedTogether
        {
            get
            {
                if (_playedTogether != null) return _playedTogether;
                var me = Me;
                _playedTogether = (from p1 in _context.MatchParticipants.AsNoTracking()
                                   join p2 in _context.MatchParticipants.AsNoTracking() on p1.MatchId equals p2.MatchId
                                   where p1.PlayerId == me && p1.Status == ParticipantStatus.Going
                                         && p2.PlayerId != me && p2.Status == ParticipantStatus.Going
                                   group p2 by p2.PlayerId into g
                                   select new { PlayerId = g.Key, Count = g.Select(x => x.MatchId).Distinct().Count() })
                                  .ToList()
                                  .ToDictionary(x => x.PlayerId, x => x.Count);
                return _playedTogether;
            }
        }

        public int PlayedTogetherWith(int id) => PlayedTogether.TryGetValue(id, out var n) ? n : 0;
    }
}
