using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;

namespace MatchBuddy.DataAccess.Concrete.EntityFramework
{
    public class EFSocialDal : ISocialDal
    {
        public const string MessageNotAllowed = ErrorCodes.Message.NotAllowed;
        public const string MessageBlocked = ErrorCodes.Message.Blocked;
        private const string PlayerNotFound = ErrorCodes.Player.NotFound;

        // ------------------------------------------------------------------
        // Yardımcılar
        // ------------------------------------------------------------------

        private static Player? ActivePlayer(MatchBuddyContext context, int playerId) =>
            context.Players.AsNoTracking().FirstOrDefault(p => p.PlayerId == playerId && !p.IsDeleted);

        private static PlayerSettings LoadSettings(MatchBuddyContext context, int playerId) =>
            context.PlayerSettings.AsNoTracking().FirstOrDefault(s => s.PlayerId == playerId)
            ?? PlayerSettings.Defaults(playerId);

        private static SocialPlayerDto ToSocial(Player p, SocialGraph graph) => new()
        {
            PlayerId = p.PlayerId,
            PlayerName = p.PlayerName,
            PlayerSurname = p.PlayerSurname,
            UserName = p.UserName,
            Size = p.Size,
            Weight = p.Weight,
            Age = p.Age,
            IsAdmin = p.IsAdmin,
            Role = p.Role,
            UserScore = p.UserScore,
            ProfilePictureUrl = p.ProfilePictureUrl,
            IFollow = graph.IFollow(p.PlayerId),
            FollowsMe = graph.FollowsMe(p.PlayerId),
            IsMutual = graph.IsMutual(p.PlayerId),
            PlayedTogether = graph.PlayedTogetherWith(p.PlayerId),
        };

        // ProfileVisibility: 0 herkes, 1 ben onu takip ediyorsam, 2 sadece kendisi
        private static bool CanSeeDetails(int viewerId, int targetId, PlayerSettings targetSettings, bool viewerFollowsTarget)
        {
            if (viewerId == targetId) return true;
            return targetSettings.ProfileVisibility switch
            {
                ProfileVisibilities.Everyone => true,
                ProfileVisibilities.Followers => viewerFollowsTarget,
                _ => false,
            };
        }

        private static bool IsBlockedEither(MatchBuddyContext context, int a, int b) =>
            context.Blocks.Any(x => (x.BlockerId == a && x.BlockedId == b) || (x.BlockerId == b && x.BlockedId == a));

        private static bool PlayedTogether(MatchBuddyContext context, int a, int b) =>
            (from p1 in context.MatchParticipants
             join p2 in context.MatchParticipants on p1.MatchId equals p2.MatchId
             where p1.PlayerId == a && p1.Status == ParticipantStatus.Going
                   && p2.PlayerId == b && p2.Status == ParticipantStatus.Going
             select p1.MatchId).Any();

        /// <summary>CanMessage(sender, recipient) kuralı (docs/social-api.md "Mesaj izni").</summary>
        private static OperationResult<bool> CanMessageCore(MatchBuddyContext context, int senderId, int recipientId)
        {
            var recipient = ActivePlayer(context, recipientId);
            if (recipient == null) return OperationResult<bool>.NotFound(PlayerNotFound);
            if (IsBlockedEither(context, senderId, recipientId)) return OperationResult<bool>.Forbidden(MessageBlocked);

            var settings = LoadSettings(context, recipientId);
            if (settings.MessagePermission == MessagePermissions.Everyone) return OperationResult<bool>.Ok(true);

            // Rezervasyon ilişkisi (işletme <-> kendi tesisindeki maçın kurucusu / Going oyuncusu): her zaman izinli
            if (VenueRules.HasReservationRelationship(context, senderId, recipientId)) return OperationResult<bool>.Ok(true);

            // Daha önce yazışmışlarsa (recipient -> sender en az bir mesaj) her zaman evet
            if (context.Messages.Any(m => m.SendPlayerId == recipientId && m.RecipientPlayerId == senderId))
                return OperationResult<bool>.Ok(true);

            var mutual = context.Follows.Any(f => f.FollowerId == senderId && f.FolloweeId == recipientId)
                         && context.Follows.Any(f => f.FollowerId == recipientId && f.FolloweeId == senderId);
            if (mutual) return OperationResult<bool>.Ok(true);

            if (settings.MessagePermission == MessagePermissions.FriendsAndPlayedWith && PlayedTogether(context, senderId, recipientId))
                return OperationResult<bool>.Ok(true);

            return OperationResult<bool>.Forbidden(MessageNotAllowed);
        }

        // ------------------------------------------------------------------
        // Takip
        // ------------------------------------------------------------------

        public OperationResult<bool> Follow(int me, int targetPlayerId)
        {
            if (me == targetPlayerId) return OperationResult<bool>.BadRequest(ErrorCodes.Social.CannotFollowSelf);

            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var target = ActivePlayer(context, targetPlayerId);
            if (target == null) return OperationResult<bool>.NotFound(PlayerNotFound);
            // Takip mantığı işletmelere uygulanmaz (docs/venues-api.md)
            if (context.Players.Any(p => p.PlayerId == me && p.Role == PlayerRoles.Owner))
                return OperationResult<bool>.Forbidden(ErrorCodes.Social.OwnerCannotFollow);
            if (target.Role == PlayerRoles.Owner)
                return OperationResult<bool>.BadRequest(ErrorCodes.Social.OwnerNotFollowable);
            if (IsBlockedEither(context, me, targetPlayerId))
                return OperationResult<bool>.Forbidden(ErrorCodes.Social.FollowBlocked);

            var followsMe = context.Follows.Any(f => f.FollowerId == targetPlayerId && f.FolloweeId == me);
            if (context.Follows.Any(f => f.FollowerId == me && f.FolloweeId == targetPlayerId))
            {
                return OperationResult<bool>.Ok(followsMe, Msg.Get(MessageKeys.Social.AlreadyFollowing));
            }

            context.Follows.Add(new Follow { FollowerId = me, FolloweeId = targetPlayerId, CreatedAt = DateTime.Now });
            ParticipationCore.AddActivity(context, ActivityType.Followed, me, targetPlayerId: targetPlayerId);

            var actor = context.Players.AsNoTracking().FirstOrDefault(p => p.PlayerId == me);
            NotificationWriter.Add(context, NotificationType.NewFollower,
                followsMe
                    ? Msg.GetDefault(MessageKeys.Social.NotifFollowedBack, ParticipationCore.DisplayName(actor))
                    : Msg.GetDefault(MessageKeys.Social.NotifNewFollower, ParticipationCore.DisplayName(actor)),
                null, null, new[] { targetPlayerId });

            try
            {
                context.SaveChanges();
                transaction.Commit();
            }
            catch (DbUpdateException)
            {
                // Eşzamanlı aynı takip isteği: unique index -> zaten takip ediliyor
                return OperationResult<bool>.Ok(followsMe, Msg.Get(MessageKeys.Social.AlreadyFollowing));
            }

            return OperationResult<bool>.Ok(followsMe, Msg.Get(followsMe ? MessageKeys.Social.NowFriends : MessageKeys.Social.Followed));
        }

        public OperationResult<bool> Unfollow(int me, int targetPlayerId)
        {
            using var context = new MatchBuddyContext();
            context.Follows.Where(f => f.FollowerId == me && f.FolloweeId == targetPlayerId).ExecuteDelete();
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Social.Unfollowed));
        }

        private OperationResult<List<SocialPlayerDto>> FollowList(int me, int playerId, bool followers)
        {
            if (playerId <= 0) playerId = me;
            using var context = new MatchBuddyContext();
            var graph = SocialGraph.Load(context, me);

            if (playerId != me)
            {
                var target = ActivePlayer(context, playerId);
                if (target == null || graph.IsBlocked(playerId))
                    return OperationResult<List<SocialPlayerDto>>.NotFound(PlayerNotFound);
                if (!CanSeeDetails(me, playerId, LoadSettings(context, playerId), graph.IFollow(playerId)))
                    return OperationResult<List<SocialPlayerDto>>.Forbidden(ErrorCodes.Social.ListPrivate);
            }

            var rows = followers
                ? (from f in context.Follows.AsNoTracking()
                   join p in context.Players.AsNoTracking() on f.FollowerId equals p.PlayerId
                   where f.FolloweeId == playerId && !p.IsDeleted
                   orderby f.CreatedAt descending
                   select p).ToList()
                : (from f in context.Follows.AsNoTracking()
                   join p in context.Players.AsNoTracking() on f.FolloweeId equals p.PlayerId
                   where f.FollowerId == playerId && !p.IsDeleted
                   orderby f.CreatedAt descending
                   select p).ToList();

            return OperationResult<List<SocialPlayerDto>>.Ok(rows
                .Where(p => !graph.IsBlocked(p.PlayerId))
                .Select(p => ToSocial(p, graph))
                .ToList());
        }

        public OperationResult<List<SocialPlayerDto>> GetFollowers(int me, int playerId) => FollowList(me, playerId, followers: true);
        public OperationResult<List<SocialPlayerDto>> GetFollowing(int me, int playerId) => FollowList(me, playerId, followers: false);

        public List<SocialPlayerDto> Discover(int me, string? query, int take)
        {
            using var context = new MatchBuddyContext();
            var graph = SocialGraph.Load(context, me);
            var blocked = graph.BlockedEither.ToList();

            var players = context.Players.AsNoTracking()
                .Where(p => p.PlayerId != me && p.IsAdmin == 0 && p.Role == PlayerRoles.Player && !p.IsDeleted && !blocked.Contains(p.PlayerId));

            var q = query?.Trim();
            if (!string.IsNullOrEmpty(q))
            {
                players = players.Where(p =>
                    p.PlayerName!.Contains(q) ||
                    p.PlayerSurname!.Contains(q) ||
                    p.UserName!.Contains(q) ||
                    (p.PlayerName + " " + p.PlayerSurname).Contains(q));
            }

            return players.ToList()
                .Select(p => ToSocial(p, graph))
                .OrderByDescending(p => p.PlayedTogether)
                .ThenByDescending(p => p.UserScore)
                .ThenBy(p => p.PlayerName)
                .ThenBy(p => p.PlayerId)
                .Take(take)
                .ToList();
        }

        public List<SocialPlayerDto> Suggestions(int me, int take)
        {
            using var context = new MatchBuddyContext();
            var graph = SocialGraph.Load(context, me);

            var excluded = new HashSet<int>(graph.Following) { me };
            excluded.UnionWith(graph.BlockedEither);

            var ordered = new List<int>();
            void AddRange(IEnumerable<int> ids)
            {
                foreach (var id in ids)
                {
                    if (!excluded.Contains(id) && !ordered.Contains(id)) ordered.Add(id);
                }
            }

            // (1) birlikte oynadığım ama takip etmediğim — playedTogether azalan
            AddRange(graph.PlayedTogether.OrderByDescending(x => x.Value).ThenBy(x => x.Key).Select(x => x.Key));
            // (2) beni takip eden ama geri takip etmediğim
            AddRange(graph.Followers.OrderByDescending(graph.PlayedTogetherWith).ThenBy(id => id));
            // (3) takip ettiklerimin takip ettikleri (ortak bağlantı sayısı azalan)
            var following = graph.Following.ToList();
            var secondDegree = context.Follows.AsNoTracking()
                .Where(f => following.Contains(f.FollowerId))
                .GroupBy(f => f.FolloweeId)
                .Select(g => new { PlayerId = g.Key, Count = g.Count() })
                .ToList()
                .OrderByDescending(x => x.Count).ThenBy(x => x.PlayerId)
                .Select(x => x.PlayerId);
            AddRange(secondDegree);

            if (ordered.Count == 0) return new List<SocialPlayerDto>();

            // Adminler ve silinmiş hesaplar önerilmez
            var players = context.Players.AsNoTracking()
                .Where(p => ordered.Contains(p.PlayerId) && p.IsAdmin == 0 && p.Role == PlayerRoles.Player && !p.IsDeleted)
                .ToList()
                .ToDictionary(p => p.PlayerId);

            return ordered
                .Where(players.ContainsKey)
                .Take(take)
                .Select(id => ToSocial(players[id], graph))
                .ToList();
        }

        // ------------------------------------------------------------------
        // Profil
        // ------------------------------------------------------------------

        public OperationResult<PlayerProfileDto> GetProfile(int me, int playerId)
        {
            if (playerId <= 0) playerId = me;
            using var context = new MatchBuddyContext();
            var graph = SocialGraph.Load(context, me);

            var player = ActivePlayer(context, playerId);
            // Beni engelleyen oyuncunun profili: 404
            if (player == null || graph.BlockedMe.Contains(playerId))
                return OperationResult<PlayerProfileDto>.NotFound(PlayerNotFound);

            var isMe = playerId == me;
            var isBlockedByMe = graph.BlockedByMe.Contains(playerId);
            var settings = LoadSettings(context, playerId);
            var statsVisible = !isBlockedByMe && CanSeeDetails(me, playerId, settings, graph.IFollow(playerId));

            var followersCount = (from f in context.Follows
                                  join p in context.Players on f.FollowerId equals p.PlayerId
                                  where f.FolloweeId == playerId && !p.IsDeleted
                                  select f.FollowId).Count();
            var followingCount = (from f in context.Follows
                                  join p in context.Players on f.FolloweeId equals p.PlayerId
                                  where f.FollowerId == playerId && !p.IsDeleted
                                  select f.FollowId).Count();

            List<TeamRefDto>? teams = null;
            if (statsVisible)
            {
                teams = (from pt in context.PlayerTeam.AsNoTracking()
                         join t in context.Teams.AsNoTracking() on pt.TeamId equals t.TeamId
                         where pt.PlayerId == playerId
                         orderby t.TeamName
                         select new TeamRefDto { TeamId = t.TeamId, TeamName = t.TeamName })
                        .ToList()
                        .DistinctBy(t => t.TeamId)
                        .ToList();
            }

            return OperationResult<PlayerProfileDto>.Ok(new PlayerProfileDto
            {
                Player = PlayerSummaryDto.FromPlayer(player),
                FollowersCount = followersCount,
                FollowingCount = followingCount,
                IFollow = graph.IFollow(playerId),
                FollowsMe = graph.FollowsMe(playerId),
                IsMutual = graph.IsMutual(playerId),
                PlayedTogether = graph.PlayedTogetherWith(playerId),
                CanMessage = !isMe && CanMessageCore(context, me, playerId).Success,
                IsBlockedByMe = isBlockedByMe,
                IsMe = isMe,
                StatsVisible = statsVisible,
                Stats = null,
                Teams = teams,
            });
        }

        // ------------------------------------------------------------------
        // Engel / şikayet
        // ------------------------------------------------------------------

        public OperationResult<bool> Block(int me, int targetPlayerId)
        {
            if (me == targetPlayerId) return OperationResult<bool>.BadRequest(ErrorCodes.Social.CannotBlockSelf);

            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            if (ActivePlayer(context, targetPlayerId) == null) return OperationResult<bool>.NotFound(PlayerNotFound);

            if (!context.Blocks.Any(b => b.BlockerId == me && b.BlockedId == targetPlayerId))
            {
                context.Blocks.Add(new Block { BlockerId = me, BlockedId = targetPlayerId, CreatedAt = DateTime.Now });
                context.SaveChanges();
            }

            // Takipler iki yönlü silinir
            context.Follows
                .Where(f => (f.FollowerId == me && f.FolloweeId == targetPlayerId) || (f.FollowerId == targetPlayerId && f.FolloweeId == me))
                .ExecuteDelete();

            // Aralarındaki bekleyen davetler iptal (red)
            var now = DateTime.Now;
            context.MatchInvites
                .Where(i => i.Status == InviteStatus.Pending &&
                            ((i.InviterId == me && i.InviteeId == targetPlayerId) || (i.InviterId == targetPlayerId && i.InviteeId == me)))
                .ExecuteUpdate(s => s.SetProperty(i => i.Status, InviteStatus.Declined).SetProperty(i => i.RespondedAt, now));
            context.TeamInvites
                .Where(i => i.Status == InviteStatus.Pending &&
                            ((i.InviterId == me && i.InviteeId == targetPlayerId) || (i.InviterId == targetPlayerId && i.InviteeId == me)))
                .ExecuteUpdate(s => s.SetProperty(i => i.Status, InviteStatus.Declined).SetProperty(i => i.RespondedAt, now));

            transaction.Commit();
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Social.Blocked));
        }

        public OperationResult<bool> Unblock(int me, int targetPlayerId)
        {
            using var context = new MatchBuddyContext();
            context.Blocks.Where(b => b.BlockerId == me && b.BlockedId == targetPlayerId).ExecuteDelete();
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Social.Unblocked));
        }

        public List<PlayerSummaryDto> GetBlocked(int me)
        {
            using var context = new MatchBuddyContext();
            return (from b in context.Blocks.AsNoTracking()
                    join p in context.Players.AsNoTracking() on b.BlockedId equals p.PlayerId
                    where b.BlockerId == me && !p.IsDeleted
                    orderby b.CreatedAt descending
                    select p).ToList()
                .Select(PlayerSummaryDto.FromPlayer)
                .ToList();
        }

        public OperationResult<bool> Report(int me, int targetPlayerId, string reason, string? details)
        {
            if (me == targetPlayerId) return OperationResult<bool>.BadRequest(ErrorCodes.Social.CannotReportSelf);

            using var context = new MatchBuddyContext();
            if (ActivePlayer(context, targetPlayerId) == null) return OperationResult<bool>.NotFound(PlayerNotFound);

            context.Reports.Add(new Report
            {
                ReporterId = me,
                ReportedPlayerId = targetPlayerId,
                Reason = reason,
                Details = details,
                Status = ReportStatus.New,
                CreatedAt = DateTime.Now,
            });
            context.SaveChanges();
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Social.ReportReceived));
        }

        // ------------------------------------------------------------------
        // Ayarlar
        // ------------------------------------------------------------------

        public PlayerSettings GetSettings(int playerId)
        {
            using var context = new MatchBuddyContext();
            return LoadSettings(context, playerId);
        }

        public void SaveSettings(PlayerSettings settings)
        {
            using var context = new MatchBuddyContext();
            var existing = context.PlayerSettings.FirstOrDefault(s => s.PlayerId == settings.PlayerId);
            if (existing == null)
            {
                context.PlayerSettings.Add(new PlayerSettings
                {
                    PlayerId = settings.PlayerId,
                    MessagePermission = settings.MessagePermission,
                    ProfileVisibility = settings.ProfileVisibility,
                    NotifyFollow = settings.NotifyFollow,
                    NotifyMatchInvite = settings.NotifyMatchInvite,
                    NotifyTeamInvite = settings.NotifyTeamInvite,
                    NotifyMatchReminder = settings.NotifyMatchReminder,
                    NotifyMatchResult = settings.NotifyMatchResult,
                    NotifyMatchUpdates = settings.NotifyMatchUpdates,
                    NotifyDeals = settings.NotifyDeals,
                    Theme = settings.Theme,
                    Language = settings.Language,
                });
            }
            else
            {
                existing.MessagePermission = settings.MessagePermission;
                existing.ProfileVisibility = settings.ProfileVisibility;
                existing.NotifyFollow = settings.NotifyFollow;
                existing.NotifyMatchInvite = settings.NotifyMatchInvite;
                existing.NotifyTeamInvite = settings.NotifyTeamInvite;
                existing.NotifyMatchReminder = settings.NotifyMatchReminder;
                existing.NotifyMatchResult = settings.NotifyMatchResult;
                existing.NotifyMatchUpdates = settings.NotifyMatchUpdates;
                existing.NotifyDeals = settings.NotifyDeals;
                existing.Theme = settings.Theme;
                existing.Language = settings.Language;
            }
            context.SaveChanges();
        }

        // ------------------------------------------------------------------
        // Mesajlar
        // ------------------------------------------------------------------

        public OperationResult<bool> CheckCanMessage(int senderId, int recipientId)
        {
            using var context = new MatchBuddyContext();
            return CanMessageCore(context, senderId, recipientId);
        }

        public List<MessageContactDto> GetMessageContacts(int me)
        {
            using var context = new MatchBuddyContext();
            var graph = SocialGraph.Load(context, me);

            // Yazıştığım herkes: kişi başına son mesaj
            var lastMessages = context.Messages.AsNoTracking()
                .Where(m => m.SendPlayerId == me || m.RecipientPlayerId == me)
                .Select(m => new
                {
                    Other = m.SendPlayerId == me ? m.RecipientPlayerId : m.SendPlayerId,
                    m.MessageId,
                    m.SendingDate,
                    m.MatchMessage,
                    HasImage = m.ImageUrl != null,
                    Mine = m.SendPlayerId == me,
                    Read = m.ReadAt != null,
                    m.IsDeleted
                })
                .ToList()
                .Where(m => m.Other.HasValue && m.Other.Value != me)
                .GroupBy(m => m.Other!.Value)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.SendingDate).ThenByDescending(m => m.MessageId).First());

            // Kişi başına okunmamış (bana gelen, okunmadı)
            var unread = context.Messages.AsNoTracking()
                .Where(m => m.RecipientPlayerId == me && m.ReadAt == null && !m.IsDeleted && m.SendPlayerId != null)
                .GroupBy(m => m.SendPlayerId!.Value)
                .Select(g => new { PlayerId = g.Key, Count = g.Count() })
                .ToDictionary(x => x.PlayerId, x => x.Count);

            // + karşılıklı takipleştiklerim (işletmeler için takip mantığı yok: sadece yazıştıkları)
            var meIsOwner = context.Players.Any(p => p.PlayerId == me && p.Role == PlayerRoles.Owner);
            var ids = lastMessages.Keys
                .Concat(meIsOwner ? Enumerable.Empty<int>() : graph.Following.Where(graph.Followers.Contains))
                .Where(id => !graph.IsBlocked(id))
                .Distinct()
                .ToList();
            if (ids.Count == 0) return new List<MessageContactDto>();

            var players = context.Players.AsNoTracking()
                .Where(p => ids.Contains(p.PlayerId) && !p.IsDeleted)
                .ToList();

            var contacts = players.Select(p => new MessageContactDto
            {
                Player = ToSocial(p, graph),
                LastMessage = lastMessages.TryGetValue(p.PlayerId, out var m)
                    ? new LastMessageDto
                    {
                        // Resimli ve metinsiz mesaj: "📷 Fotoğraf" (docs/media-api.md)
                        Text = m.IsDeleted ? Msg.Get(MessageKeys.Message.LabelDeleted)
                            : m.HasImage && string.IsNullOrWhiteSpace(m.MatchMessage) ? Msg.Get(MessageKeys.Message.LabelPhotoPreview) : m.MatchMessage,
                        SendingDate = m.SendingDate,
                        Mine = m.Mine,
                        HasImage = m.HasImage,
                        Read = m.Mine && m.Read,
                    }
                    : null,
                UnreadCount = unread.TryGetValue(p.PlayerId, out var u) ? u : 0,
            }).ToList();

            // Son mesaj tarihi azalan; mesajı olmayanlar sonda, ada göre
            return contacts.Where(c => c.LastMessage != null)
                .OrderByDescending(c => c.LastMessage!.SendingDate)
                .Concat(contacts.Where(c => c.LastMessage == null)
                    .OrderBy(c => c.Player.PlayerName, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(c => c.Player.PlayerSurname, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(c => c.Player.PlayerId))
                .ToList();
        }

        // ------------------------------------------------------------------
        // Akış
        // ------------------------------------------------------------------

        public List<ActivityDto> GetFeed(int me, int take, DateTime? before)
        {
            using var context = new MatchBuddyContext();
            var graph = SocialGraph.Load(context, me);
            var actorIds = graph.Following.Where(id => !graph.IsBlocked(id)).ToList();
            if (actorIds.Count == 0) return new List<ActivityDto>();
            var blocked = graph.BlockedEither.ToList();

            var query = context.Activities.AsNoTracking()
                .Where(a => actorIds.Contains(a.ActorPlayerId)
                            && (a.TargetPlayerId == null || !blocked.Contains(a.TargetPlayerId.Value)));
            if (before.HasValue)
            {
                var b = before.Value;
                query = query.Where(a => a.CreatedAt < b);
            }

            var activities = query
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.ActivityId)
                .Take(take)
                .ToList();
            if (activities.Count == 0) return new List<ActivityDto>();

            var playerIds = activities.Select(a => a.ActorPlayerId)
                .Concat(activities.Where(a => a.TargetPlayerId.HasValue).Select(a => a.TargetPlayerId!.Value))
                .Distinct().ToList();
            var players = context.Players.AsNoTracking()
                .Where(p => playerIds.Contains(p.PlayerId))
                .ToList()
                .ToDictionary(p => p.PlayerId, PlayerSummaryDto.FromPlayer);

            var matchIds = activities.Where(a => a.MatchId.HasValue).Select(a => a.MatchId!.Value).Distinct().ToList();
            var matches = (from m in context.Matchs.AsNoTracking()
                           join s in context.Stadiums.AsNoTracking() on m.StadiumId equals s.StadiumId into ms
                           from s in ms.DefaultIfEmpty()
                           where matchIds.Contains(m.MatchId)
                           select new ActivityMatchDto
                           {
                               MatchId = m.MatchId,
                               MatchName = m.MatchName,
                               MatchDate = m.MatchDate,
                               HomeScore = m.HomeScore,
                               AwayScore = m.AwayScore,
                               StadiumName = s != null ? s.StadiumName : null,
                           }).ToList().ToDictionary(m => m.MatchId);

            var teamIds = activities.Where(a => a.TeamId.HasValue).Select(a => a.TeamId!.Value).Distinct().ToList();
            var teams = context.Teams.AsNoTracking()
                .Where(t => teamIds.Contains(t.TeamId))
                .Select(t => new TeamRefDto { TeamId = t.TeamId, TeamName = t.TeamName })
                .ToList()
                .ToDictionary(t => t.TeamId);

            return activities
                .Where(a => players.ContainsKey(a.ActorPlayerId))
                .Select(a => new ActivityDto
                {
                    ActivityId = a.ActivityId,
                    Type = a.Type,
                    CreatedAt = a.CreatedAt,
                    Actor = players[a.ActorPlayerId],
                    Match = a.MatchId.HasValue && matches.TryGetValue(a.MatchId.Value, out var m) ? m : null,
                    Team = a.TeamId.HasValue && teams.TryGetValue(a.TeamId.Value, out var t) ? t : null,
                    Target = a.TargetPlayerId.HasValue && players.TryGetValue(a.TargetPlayerId.Value, out var tp) ? tp : null,
                    Value = a.Value,
                })
                .ToList();
        }

        // ------------------------------------------------------------------
        // Hesap silme
        // ------------------------------------------------------------------

        public bool DeleteAccount(int playerId, string randomPasswordHash)
        {
            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var player = context.Players.FirstOrDefault(p => p.PlayerId == playerId && !p.IsDeleted);
            if (player == null) return false;

            // Gelecek maçlardaki katılımlar Leave mantığıyla bırakılır (yedekten yükseltme dahil)
            var now = DateTime.Now;
            var futureMatchIds = (from p in context.MatchParticipants
                                  join m in context.Matchs on p.MatchId equals m.MatchId
                                  where p.PlayerId == playerId
                                        && (p.Status == ParticipantStatus.Going || p.Status == ParticipantStatus.Waitlist)
                                        && m.MatchDate > now
                                  select p.MatchId).Distinct().ToList();
            foreach (var matchId in futureMatchIds.OrderBy(id => id))
            {
                var match = ParticipationCore.LockMatch(context, matchId);
                if (match == null) continue;
                ParticipationCore.Leave(context, match, playerId);
                context.SaveChanges();
            }

            // Takipler, engeller, ayarlar ve davetler silinir
            context.Follows.Where(f => f.FollowerId == playerId || f.FolloweeId == playerId).ExecuteDelete();
            context.Blocks.Where(b => b.BlockerId == playerId || b.BlockedId == playerId).ExecuteDelete();
            context.PlayerSettings.Where(s => s.PlayerId == playerId).ExecuteDelete();
            context.MatchInvites.Where(i => i.InviterId == playerId || i.InviteeId == playerId).ExecuteDelete();
            context.TeamInvites.Where(i => i.InviterId == playerId || i.InviteeId == playerId).ExecuteDelete();
            // Oturumlar, sosyal giriş bağlantıları ve tek kullanımlık kodlar (docs/auth-api.md)
            context.RefreshTokens.Where(t => t.PlayerId == playerId).ExecuteDelete();
            context.ExternalLogins.Where(l => l.PlayerId == playerId).ExecuteDelete();
            context.AuthCodes.Where(c => c.PlayerId == playerId).ExecuteDelete();

            // Anonimleştirme (geçmiş maç kayıtları korunur)
            player.PlayerName = Msg.GetDefault(MessageKeys.Player.LabelDeletedName);
            player.PlayerSurname = Msg.GetDefault(MessageKeys.Player.LabelDeletedSurname);
            player.UserName = $"silinmis_{playerId}";
            player.Email = string.Empty;
            player.PhoneNumber = string.Empty;
            player.Address = string.Empty;
            player.ProfilePictureUrl = null;
            player.ProfileMediaId = null; // medya dosyaları API katmanında (MediaService) silinir
            player.Password = randomPasswordHash;
            player.IsDeleted = true;
            player.DeletedAt = now;
            player.EmailConfirmed = false;
            player.PhoneConfirmed = false;
            player.MarketingConsent = false;

            context.SaveChanges();
            transaction.Commit();
            return true;
        }

        // ------------------------------------------------------------------
        // Oyuncu listesi
        // ------------------------------------------------------------------

        public List<Player> GetVisiblePlayers(int viewerPlayerId)
        {
            using var context = new MatchBuddyContext();
            var blocked = SocialGraph.Load(context, viewerPlayerId).BlockedEither.ToList();
            // İşletme hesapları oyuncu listelerinde görünmez
            return context.Players.AsNoTracking()
                .Where(p => !p.IsDeleted && p.Role != PlayerRoles.Owner && !blocked.Contains(p.PlayerId))
                .ToList();
        }

        public HashSet<int> OwnerIds(IEnumerable<int> playerIds)
        {
            var ids = playerIds.Distinct().ToList();
            if (ids.Count == 0) return new HashSet<int>();
            using var context = new MatchBuddyContext();
            return context.Players.AsNoTracking()
                .Where(p => ids.Contains(p.PlayerId) && p.Role == PlayerRoles.Owner)
                .Select(p => p.PlayerId)
                .ToHashSet();
        }

        // ------------------------------------------------------------------
        // Admin: şikayetler
        // ------------------------------------------------------------------

        public List<AdminReportDto> GetReports(byte? status)
        {
            using var context = new MatchBuddyContext();
            var query = context.Reports.AsNoTracking();
            if (status.HasValue)
            {
                var st = status.Value;
                query = query.Where(r => r.Status == st);
            }
            var reports = query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.ReportId).ToList();
            if (reports.Count == 0) return new List<AdminReportDto>();

            var reportedIds = reports.Select(r => r.ReportedPlayerId).Distinct().ToList();
            var counts = context.Reports.AsNoTracking()
                .Where(r => reportedIds.Contains(r.ReportedPlayerId))
                .GroupBy(r => r.ReportedPlayerId)
                .Select(g => new { PlayerId = g.Key, Count = g.Count() })
                .ToList()
                .ToDictionary(x => x.PlayerId, x => x.Count);

            var playerIds = reportedIds.Concat(reports.Select(r => r.ReporterId)).Distinct().ToList();
            var players = context.Players.AsNoTracking()
                .Where(p => playerIds.Contains(p.PlayerId))
                .ToList()
                .ToDictionary(p => p.PlayerId, PlayerSummaryDto.FromPlayer);

            return reports
                .Where(r => players.ContainsKey(r.ReporterId) && players.ContainsKey(r.ReportedPlayerId))
                .Select(r => new AdminReportDto
                {
                    ReportId = r.ReportId,
                    Reason = r.Reason,
                    Details = r.Details,
                    Status = r.Status,
                    CreatedAt = r.CreatedAt,
                    Reporter = players[r.ReporterId],
                    Reported = players[r.ReportedPlayerId],
                    ReportedCount = counts.TryGetValue(r.ReportedPlayerId, out var c) ? c : 0,
                })
                .ToList();
        }

        public OperationResult<bool> ResolveReport(int reportId)
        {
            using var context = new MatchBuddyContext();
            var report = context.Reports.FirstOrDefault(r => r.ReportId == reportId);
            if (report == null) return OperationResult<bool>.NotFound(ErrorCodes.Social.ReportNotFound);
            report.Status = ReportStatus.Reviewed;
            context.SaveChanges();
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Social.ReportResolved));
        }
    }
}
