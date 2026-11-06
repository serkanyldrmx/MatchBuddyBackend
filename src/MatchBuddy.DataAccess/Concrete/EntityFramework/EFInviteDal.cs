using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.DataAccess.Abstract;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using static MatchBuddy.DataAccess.Concrete.EntityFramework.ParticipationCore;

namespace MatchBuddy.DataAccess.Concrete.EntityFramework
{
    public class EFInviteDal : IInviteDal
    {
        private const string InviteNotFound = ErrorCodes.Invite.NotFound;

        public OperationResult<InviteResultDto> InviteToMatch(int me, bool isAdmin, int matchId, List<int> playerIds)
        {
            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var match = LockMatch(context, matchId);
            if (match == null) return OperationResult<InviteResultDto>.NotFound();
            if (match.IsActive == RejectedStatus)
                return OperationResult<InviteResultDto>.BadRequest(ErrorCodes.Invite.MatchRejected);
            if (IsPast(match))
                return OperationResult<InviteResultDto>.BadRequest(ErrorCodes.Invite.MatchPast);

            var participants = context.MatchParticipants.Where(p => p.MatchId == matchId).ToList();
            var amGoing = participants.Any(p => p.PlayerId == me && p.Status == ParticipantStatus.Going);
            if (!amGoing && !CanManage(context, match, me, isAdmin))
                return OperationResult<InviteResultDto>.Forbidden(ErrorCodes.Invite.NotPlayingInMatch);

            var requested = (playerIds ?? new List<int>()).Distinct().ToList();
            var graph = SocialGraph.Load(context, me);
            // İşletme hesapları davet edilemez
            var active = context.Players.AsNoTracking()
                .Where(p => requested.Contains(p.PlayerId) && !p.IsDeleted && p.Role != PlayerRoles.Owner)
                .Select(p => p.PlayerId)
                .ToHashSet();
            var activeParticipants = participants
                .Where(p => p.Status == ParticipantStatus.Going || p.Status == ParticipantStatus.Waitlist)
                .Select(p => p.PlayerId)
                .ToHashSet();
            var existing = context.MatchInvites
                .Where(i => i.MatchId == matchId && requested.Contains(i.InviteeId))
                .ToList()
                .ToDictionary(i => i.InviteeId);

            var now = DateTime.Now;
            var invited = new List<int>();
            var skipped = 0;
            foreach (var id in requested)
            {
                existing.TryGetValue(id, out var row);
                if (id == me || !active.Contains(id) || graph.IsBlocked(id)
                    || activeParticipants.Contains(id) || row?.Status == InviteStatus.Pending)
                {
                    skipped++;
                    continue;
                }

                if (row == null)
                {
                    context.MatchInvites.Add(new MatchInvite
                    {
                        MatchId = matchId, InviterId = me, InviteeId = id,
                        Status = InviteStatus.Pending, CreatedAt = now,
                    });
                }
                else
                {
                    // Daha önce yanıtlanmış davet yeniden açılır (unique: MatchId + InviteeId)
                    row.InviterId = me;
                    row.Status = InviteStatus.Pending;
                    row.CreatedAt = now;
                    row.RespondedAt = null;
                    row.Side = null; // bireysel davet: taraf yok
                }
                invited.Add(id);
            }

            if (invited.Count > 0)
            {
                var inviter = context.Players.AsNoTracking().FirstOrDefault(p => p.PlayerId == me);
                NotificationWriter.Add(context, NotificationType.MatchInvite,
                    Msg.GetDefault(MessageKeys.Invite.NotifMatch, DisplayName(inviter), match.MatchName, match.MatchDate),
                    matchId, null, invited);
            }

            context.SaveChanges();
            transaction.Commit();
            return OperationResult<InviteResultDto>.Ok(new InviteResultDto { Invited = invited.Count, Skipped = skipped },
                Msg.Get(invited.Count > 0 ? MessageKeys.Invite.Sent : MessageKeys.Invite.NoneToSend));
        }

        public OperationResult<InviteResultDto> InviteToTeam(int me, int teamId, List<int> playerIds)
        {
            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var team = context.Teams.AsNoTracking().FirstOrDefault(t => t.TeamId == teamId);
            if (team == null) return OperationResult<InviteResultDto>.NotFound(ErrorCodes.Team.NotFound);

            var members = context.PlayerTeam.Where(pt => pt.TeamId == teamId).Select(pt => pt.PlayerId).ToHashSet();
            if (!members.Contains(me))
                return OperationResult<InviteResultDto>.Forbidden(ErrorCodes.Invite.NotTeamMember);

            var requested = (playerIds ?? new List<int>()).Distinct().ToList();
            var graph = SocialGraph.Load(context, me);
            var active = context.Players.AsNoTracking()
                .Where(p => requested.Contains(p.PlayerId) && !p.IsDeleted && p.Role != PlayerRoles.Owner)
                .Select(p => p.PlayerId)
                .ToHashSet();
            var existing = context.TeamInvites
                .Where(i => i.TeamId == teamId && requested.Contains(i.InviteeId))
                .ToList()
                .ToDictionary(i => i.InviteeId);

            var now = DateTime.Now;
            var invited = new List<int>();
            var skipped = 0;
            foreach (var id in requested)
            {
                existing.TryGetValue(id, out var row);
                if (id == me || !active.Contains(id) || graph.IsBlocked(id)
                    || members.Contains(id) || row?.Status == InviteStatus.Pending)
                {
                    skipped++;
                    continue;
                }

                if (row == null)
                {
                    context.TeamInvites.Add(new TeamInvite
                    {
                        TeamId = teamId, InviterId = me, InviteeId = id,
                        Status = InviteStatus.Pending, CreatedAt = now,
                    });
                }
                else
                {
                    row.InviterId = me;
                    row.Status = InviteStatus.Pending;
                    row.CreatedAt = now;
                    row.RespondedAt = null;
                }
                invited.Add(id);
            }

            if (invited.Count > 0)
            {
                var inviter = context.Players.AsNoTracking().FirstOrDefault(p => p.PlayerId == me);
                NotificationWriter.Add(context, NotificationType.TeamInvite,
                    Msg.GetDefault(MessageKeys.Invite.NotifTeam, DisplayName(inviter), team.TeamName),
                    null, null, invited);
            }

            context.SaveChanges();
            transaction.Commit();
            return OperationResult<InviteResultDto>.Ok(new InviteResultDto { Invited = invited.Count, Skipped = skipped },
                Msg.Get(invited.Count > 0 ? MessageKeys.Invite.Sent : MessageKeys.Invite.NoneToSend));
        }

        public MyInvitesDto GetMyInvites(int me)
        {
            using var context = new MatchBuddyContext();
            var blocked = SocialGraph.Load(context, me).BlockedEither.ToList();

            var matchRows = (from i in context.MatchInvites.AsNoTracking()
                             join m in context.Matchs.AsNoTracking() on i.MatchId equals m.MatchId
                             join p in context.Players.AsNoTracking() on i.InviterId equals p.PlayerId
                             join s in context.Stadiums.AsNoTracking() on m.StadiumId equals s.StadiumId into ms
                             from s in ms.DefaultIfEmpty()
                             where i.InviteeId == me && i.Status == InviteStatus.Pending
                                   && !p.IsDeleted && !blocked.Contains(i.InviterId)
                             orderby i.CreatedAt descending, i.InviteId descending
                             select new { Invite = i, Match = m, Inviter = p, StadiumName = s != null ? s.StadiumName : null })
                            .ToList();

            var matchIds = matchRows.Select(r => r.Match.MatchId).Distinct().ToList();
            // Going + misafir
            var goingCounts = RosterCounts(context, matchIds);

            var teamRows = (from i in context.TeamInvites.AsNoTracking()
                            join t in context.Teams.AsNoTracking() on i.TeamId equals t.TeamId
                            join p in context.Players.AsNoTracking() on i.InviterId equals p.PlayerId
                            where i.InviteeId == me && i.Status == InviteStatus.Pending
                                  && !p.IsDeleted && !blocked.Contains(i.InviterId)
                            orderby i.CreatedAt descending, i.InviteId descending
                            select new { Invite = i, Team = t, Inviter = p })
                           .ToList();

            var teamIds = teamRows.Select(r => r.Team.TeamId).Distinct().ToList();
            var memberCounts = context.PlayerTeam.AsNoTracking()
                .Where(pt => teamIds.Contains(pt.TeamId))
                .GroupBy(pt => pt.TeamId)
                .Select(g => new { TeamId = g.Key, Count = g.Count() })
                .ToList()
                .ToDictionary(x => x.TeamId, x => x.Count);

            return new MyInvitesDto
            {
                MatchInvites = matchRows.Select(r => new MatchInviteDto
                {
                    InviteId = r.Invite.InviteId,
                    CreatedAt = r.Invite.CreatedAt,
                    Inviter = PlayerSummaryDto.FromPlayer(r.Inviter),
                    Match = new InviteMatchInfoDto
                    {
                        MatchId = r.Match.MatchId,
                        MatchName = r.Match.MatchName,
                        MatchDate = r.Match.MatchDate,
                        StadiumName = r.StadiumName,
                        Capacity = r.Match.UserCount,
                        GoingCount = goingCounts.TryGetValue(r.Match.MatchId, out var c) ? c : 0,
                        IsActive = r.Match.IsActive,
                    },
                }).ToList(),
                TeamInvites = teamRows.Select(r => new TeamInviteDto
                {
                    InviteId = r.Invite.InviteId,
                    CreatedAt = r.Invite.CreatedAt,
                    Inviter = PlayerSummaryDto.FromPlayer(r.Inviter),
                    Team = new InviteTeamInfoDto
                    {
                        TeamId = r.Team.TeamId,
                        TeamName = r.Team.TeamName,
                        MemberCount = memberCounts.TryGetValue(r.Team.TeamId, out var c) ? c : 0,
                    },
                }).ToList(),
            };
        }

        public int GetPendingCount(int me)
        {
            using var context = new MatchBuddyContext();
            var blocked = SocialGraph.Load(context, me).BlockedEither.ToList();

            var matchCount = (from i in context.MatchInvites
                              join p in context.Players on i.InviterId equals p.PlayerId
                              where i.InviteeId == me && i.Status == InviteStatus.Pending
                                    && !p.IsDeleted && !blocked.Contains(i.InviterId)
                              select i.InviteId).Count();
            var teamCount = (from i in context.TeamInvites
                             join p in context.Players on i.InviterId equals p.PlayerId
                             where i.InviteeId == me && i.Status == InviteStatus.Pending
                                   && !p.IsDeleted && !blocked.Contains(i.InviterId)
                             select i.InviteId).Count();
            return matchCount + teamCount;
        }

        public OperationResult<byte?> RespondMatch(int me, int inviteId, bool accept)
        {
            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var invite = context.MatchInvites.FirstOrDefault(i => i.InviteId == inviteId);
            if (invite == null) return OperationResult<byte?>.NotFound(InviteNotFound);
            if (invite.InviteeId != me) return OperationResult<byte?>.Forbidden(ErrorCodes.Invite.NotYours);
            if (invite.Status != InviteStatus.Pending) return OperationResult<byte?>.BadRequest(ErrorCodes.Invite.AlreadyResponded);

            if (!accept)
            {
                invite.Status = InviteStatus.Declined;
                invite.RespondedAt = DateTime.Now;
                context.SaveChanges();
                transaction.Commit();
                return OperationResult<byte?>.Ok(null, Msg.Get(MessageKeys.Invite.Declined));
            }

            var match = LockMatch(context, invite.MatchId);
            if (match == null) return OperationResult<byte?>.NotFound();
            if (IsPast(match)) return OperationResult<byte?>.BadRequest(ErrorCodes.Invite.PastMatchAccept);

            // Zaten kadrodaysa (davetten sonra kendisi katıldıysa) mevcut durum korunur
            var current = context.MatchParticipants
                .FirstOrDefault(p => p.MatchId == match.MatchId && p.PlayerId == me
                                     && (p.Status == ParticipantStatus.Going || p.Status == ParticipantStatus.Waitlist));
            byte status;
            string message;
            if (current != null)
            {
                status = current.Status;
                message = Msg.Get(status == ParticipantStatus.Going ? MessageKeys.Invite.AlreadyGoing : MessageKeys.Invite.AlreadyWaitlist);
            }
            else
            {
                // Join mantığı (Going / Waitlist, aktivite, kurucuya bildirim)
                // Takım daveti: oyuncu davetteki tarafa yerleşir (taraf doluysa normal Join kuralı)
                var joinResult = Join(context, match, me, invite.Side);
                if (!joinResult.Success)
                {
                    // Kod ve argümanlar korunur; durum eskisi gibi 404 / 403 / diğerleri 400
                    var failStatus = joinResult.Status is OperationStatus.NotFound or OperationStatus.Forbidden
                        ? joinResult.Status : OperationStatus.BadRequest;
                    return OperationResult<byte?>.Fail(failStatus, joinResult.Code ?? ErrorCodes.Common.OperationFailed, joinResult.Args);
                }
                status = joinResult.Data;
                message = joinResult.Message;
            }

            invite.Status = InviteStatus.Accepted;
            invite.RespondedAt = DateTime.Now;
            context.SaveChanges();
            transaction.Commit();
            return OperationResult<byte?>.Ok(status, message);
        }

        public OperationResult<byte?> RespondTeam(int me, int inviteId, bool accept)
        {
            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var invite = context.TeamInvites.FirstOrDefault(i => i.InviteId == inviteId);
            if (invite == null) return OperationResult<byte?>.NotFound(InviteNotFound);
            if (invite.InviteeId != me) return OperationResult<byte?>.Forbidden(ErrorCodes.Invite.NotYours);
            if (invite.Status != InviteStatus.Pending) return OperationResult<byte?>.BadRequest(ErrorCodes.Invite.AlreadyResponded);

            invite.Status = accept ? InviteStatus.Accepted : InviteStatus.Declined;
            invite.RespondedAt = DateTime.Now;

            if (accept && !context.PlayerTeam.Any(pt => pt.TeamId == invite.TeamId && pt.PlayerId == me))
            {
                context.PlayerTeam.Add(new PlayerTeam { TeamId = invite.TeamId, PlayerId = me });
            }

            context.SaveChanges();
            transaction.Commit();
            return OperationResult<byte?>.Ok(null, Msg.Get(accept ? MessageKeys.Invite.TeamJoined : MessageKeys.Invite.Declined));
        }
    }
}
