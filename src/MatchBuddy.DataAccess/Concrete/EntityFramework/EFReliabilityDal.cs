using MatchBuddy.Core.Utilities.Results;
using MatchBuddy.Entities.DTOs;
using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using static MatchBuddy.DataAccess.Concrete.EntityFramework.ParticipationCore;

namespace MatchBuddy.DataAccess.Concrete.EntityFramework
{
    public interface IReliabilityDal
    {
        OperationResult<LeavePreviewDto> LeavePreview(int matchId, int playerId);
        OperationResult<int> MarkNoShows(int matchId, int actorPlayerId, bool isAdmin, IReadOnlyCollection<int> playerIds);
        OperationResult<int> AcceptExcuse(int matchId, int actorPlayerId, bool isAdmin, int playerId);
        ReliabilitySummaryDto Summary(int playerId, bool includeHistory);
        Dictionary<int, ReliabilityBadge> Badges(IEnumerable<int> playerIds);
        List<MatchWithdrawalDto> Withdrawals(int matchId);
        int SettleDue();
    }

    /// <summary>Güvenilirlik puanı işlemleri — docs/reliability-api.md.</summary>
    public class EFReliabilityDal : IReliabilityDal
    {
        public OperationResult<LeavePreviewDto> LeavePreview(int matchId, int playerId)
        {
            using var context = new MatchBuddyContext();
            var match = context.Matchs.AsNoTracking().FirstOrDefault(m => m.MatchId == matchId);
            if (match == null) return OperationResult<LeavePreviewDto>.NotFound(ErrorCodes.Match.NotFound);
            var mine = context.MatchParticipants.AsNoTracking().FirstOrDefault(p => p.MatchId == matchId && p.PlayerId == playerId);
            if (mine == null || mine.Status == ParticipantStatus.Declined)
                return OperationResult<LeavePreviewDto>.BadRequest(ErrorCodes.Match.NotParticipant);

            var isGoing = mine.Status == ParticipantStatus.Going;
            var penalty = ReliabilityCore.PenaltyFor(match, DateTime.Now);
            var score = context.Players.AsNoTracking().Where(p => p.PlayerId == playerId).Select(p => p.ReliabilityScore).FirstOrDefault();
            return OperationResult<LeavePreviewDto>.Ok(new LeavePreviewDto
            {
                HoursLeft = Math.Round(Math.Max(penalty.HoursLeft, 0), 1),
                Kind = isGoing ? penalty.Kind : "none",
                Penalty = isGoing ? Math.Min(penalty.Penalty, score) : 0,
                CurrentScore = score,
                IsGoing = isGoing,
            });
        }

        /// <summary>
        /// Kurucu, maç başladıktan sonra pencere içinde gelmeyenleri işaretler. Liste tam kümedir:
        /// listede olmayan önceki işaretler geri alınır (tekrar çağrı = güncelleme). Misafirler bu listeye girmez.
        /// </summary>
        public OperationResult<int> MarkNoShows(int matchId, int actorPlayerId, bool isAdmin, IReadOnlyCollection<int> playerIds)
        {
            using var context = new MatchBuddyContext();
            using var transaction = context.Database.BeginTransaction();
            var match = LockMatch(context, matchId);
            if (match == null) return OperationResult<int>.NotFound(ErrorCodes.Match.NotFound);
            if (!IsOrganizer(match, actorPlayerId, isAdmin)) return OperationResult<int>.Forbidden(ErrorCodes.Match.OrganizerOnly);
            if (!IsPast(match)) return OperationResult<int>.BadRequest(ErrorCodes.Reliability.NoShowNotStarted);
            var window = P.Int(ParameterKeys.Reliability.NoShowMarkWindowHours);
            if (match.ReliabilitySettledAt != null || match.MatchDate!.Value.AddHours(window) < DateTime.Now)
                return OperationResult<int>.BadRequest(ErrorCodes.Reliability.NoShowWindowClosed, window);

            var ids = playerIds.Distinct().Where(id => id != actorPlayerId).ToHashSet();
            var going = context.MatchParticipants.Where(p => p.MatchId == matchId && p.Status == ParticipantStatus.Going)
                .Select(p => p.PlayerId).ToHashSet();
            if (ids.Any(id => !going.Contains(id)))
                return OperationResult<int>.BadRequest(ErrorCodes.Reliability.NoShowNotParticipant);

            var existing = context.ReliabilityEvents
                .Where(e => e.MatchId == matchId && e.Type == ReliabilityEventType.NoShow).ToList();
            var touched = new HashSet<int>();
            // Listeden çıkarılanlar: işaret silinir (kurucu düzeltmesi, kalıcı iz gerekmez)
            foreach (var e in existing.Where(e => !ids.Contains(e.PlayerId)))
            {
                context.ReliabilityEvents.Remove(e);
                touched.Add(e.PlayerId);
            }
            var penalty = P.Int(ParameterKeys.Reliability.NoShowPenalty);
            var newlyMarked = new List<int>();
            foreach (var id in ids.Where(id => existing.All(e => e.PlayerId != id)))
            {
                ReliabilityCore.AddEvent(context, id, matchId, ReliabilityEventType.NoShow, -penalty);
                touched.Add(id);
                newlyMarked.Add(id);
            }
            if (newlyMarked.Count > 0)
            {
                AddNotification(context, NotificationType.MatchResult,
                    Msg.Get(MessageKeys.Reliability.NotifNoShow, match.MatchName ?? string.Empty, penalty), matchId, newlyMarked);
            }
            context.SaveChanges();
            foreach (var id in touched) ReliabilityCore.Recompute(context, id);
            context.SaveChanges();
            transaction.Commit();
            return OperationResult<int>.Ok(ids.Count, Msg.Get(MessageKeys.Reliability.NoShowsSaved, ids.Count));
        }

        /// <summary>Kurucu, oyuncunun bu maçtaki iptal/gelmedi cezasını mazeretli sayar → olaylar geri alınır, puan iade.</summary>
        public OperationResult<int> AcceptExcuse(int matchId, int actorPlayerId, bool isAdmin, int playerId)
        {
            using var context = new MatchBuddyContext();
            var match = context.Matchs.AsNoTracking().FirstOrDefault(m => m.MatchId == matchId);
            if (match == null) return OperationResult<int>.NotFound(ErrorCodes.Match.NotFound);
            if (!IsOrganizer(match, actorPlayerId, isAdmin)) return OperationResult<int>.Forbidden(ErrorCodes.Match.OrganizerOnly);
            if (playerId == actorPlayerId) return OperationResult<int>.BadRequest(ErrorCodes.Reliability.CannotExcuseSelf);

            var events = context.ReliabilityEvents
                .Where(e => e.MatchId == matchId && e.PlayerId == playerId && e.ReversedAt == null && e.Type != ReliabilityEventType.Attended)
                .ToList();
            if (events.Count == 0) return OperationResult<int>.BadRequest(ErrorCodes.Reliability.NoPenalty);

            var before = context.Players.Where(p => p.PlayerId == playerId).Select(p => p.ReliabilityScore).First();
            foreach (var e in events)
            {
                e.ReversedAt = DateTime.Now;
                e.ReversedByPlayerId = actorPlayerId;
            }
            context.SaveChanges();
            var after = ReliabilityCore.Recompute(context, playerId);
            AddNotification(context, NotificationType.MatchResult,
                Msg.Get(MessageKeys.Reliability.NotifExcuseAccepted, match.MatchName ?? string.Empty, Math.Max(after - before, 0)),
                matchId, new[] { playerId });
            context.SaveChanges();

            var name = context.Players.Where(p => p.PlayerId == playerId)
                .Select(p => (p.PlayerName + " " + p.PlayerSurname).Trim()).FirstOrDefault() ?? string.Empty;
            return OperationResult<int>.Ok(after, Msg.Get(MessageKeys.Reliability.ExcuseAccepted, name));
        }

        public ReliabilitySummaryDto Summary(int playerId, bool includeHistory)
        {
            using var context = new MatchBuddyContext();
            var badge = ReliabilityCore.Badge(context, playerId);

            // Son 10 işlenmiş maç / iptal
            var recent = context.ReliabilityEvents.AsNoTracking()
                .Where(e => e.PlayerId == playerId && e.Type != ReliabilityEventType.OrganizerLateCancel)
                .OrderByDescending(e => e.CreatedAt).Take(10)
                .Select(e => new { e.Type, e.ReversedAt }).ToList();

            var dto = new ReliabilitySummaryDto
            {
                Score = badge.Score,
                Level = badge.Level,
                CompletedMatches = badge.CompletedMatches,
                Attended = recent.Count(e => e.Type == ReliabilityEventType.Attended),
                LateCancels = recent.Count(e => e.ReversedAt == null
                    && (e.Type == ReliabilityEventType.LateCancel || e.Type == ReliabilityEventType.VeryLateCancel)),
                NoShows = recent.Count(e => e.ReversedAt == null && e.Type == ReliabilityEventType.NoShow),
            };
            if (includeHistory)
            {
                var rows = context.ReliabilityEvents.AsNoTracking().Where(e => e.PlayerId == playerId)
                    .OrderByDescending(e => e.CreatedAt).Take(50).ToList();
                var matchIds = rows.Where(r => r.MatchId != null).Select(r => r.MatchId!.Value).Distinct().ToList();
                var names = context.Matchs.AsNoTracking().Where(m => matchIds.Contains(m.MatchId))
                    .ToDictionary(m => m.MatchId, m => m.MatchName);
                dto.History = rows.Select(r => new ReliabilityHistoryItemDto
                {
                    EventId = r.ReliabilityEventId,
                    MatchId = r.MatchId,
                    MatchName = r.MatchId != null && names.TryGetValue(r.MatchId.Value, out var n) ? n : null,
                    Type = ReliabilityEventType.Name(r.Type),
                    Delta = r.Delta,
                    ReasonCode = r.ReasonCode,
                    CreatedAt = r.CreatedAt,
                    Reversed = r.ReversedAt != null,
                }).ToList();
            }
            return dto;
        }

        public Dictionary<int, ReliabilityBadge> Badges(IEnumerable<int> playerIds)
        {
            using var context = new MatchBuddyContext();
            return ReliabilityCore.Badges(context, playerIds);
        }

        public List<MatchWithdrawalDto> Withdrawals(int matchId)
        {
            using var context = new MatchBuddyContext();
            return context.ReliabilityEvents.AsNoTracking()
                .Where(e => e.MatchId == matchId && e.Type != ReliabilityEventType.Attended && e.Type != ReliabilityEventType.OrganizerLateCancel)
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new MatchWithdrawalDto
                {
                    EventId = e.ReliabilityEventId,
                    PlayerId = e.PlayerId,
                    PlayerName = e.Player.PlayerName,
                    PlayerSurname = e.Player.PlayerSurname,
                    UserName = e.Player.UserName,
                    ProfilePictureUrl = e.Player.ProfilePictureUrl,
                    Type = e.Type == ReliabilityEventType.NoShow ? "noShow" : e.Type == ReliabilityEventType.VeryLateCancel ? "veryLateCancel" : "lateCancel",
                    Penalty = -e.Delta,
                    ReasonCode = e.ReasonCode,
                    Note = e.Note,
                    CreatedAt = e.CreatedAt,
                    Excused = e.ReversedAt != null,
                })
                .ToList();
        }

        public int SettleDue()
        {
            using var context = new MatchBuddyContext();
            return ReliabilityCore.SettleDue(context);
        }

        private static bool IsOrganizer(Match match, int actorPlayerId, bool isAdmin) =>
            isAdmin || (match.CreatedByPlayerId.HasValue && match.CreatedByPlayerId.Value == actorPlayerId);
    }
}
