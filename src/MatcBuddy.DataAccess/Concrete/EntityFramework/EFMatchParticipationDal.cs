using MatcBuddy.Core.DataAccess.EntityFramework;
using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.DataAccess.Abstract;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using static MatcBuddy.DataAccess.Concrete.EntityFramework.ParticipationCore;

namespace MatcBuddy.DataAccess.Concrete.EntityFramework
{
    public class EFMatchParticipationDal : EfEntityRepositoryBase<MatchParticipant, MatcBuddyContext>, IMatchParticipationDal
    {
        // ------------------------------------------------------------------
        // Yardımcılar (kadro kuralları: ParticipationCore)
        // ------------------------------------------------------------------

        // Maç başına en çok oyu alan (beraberlikte en düşük playerId)
        private static Dictionary<int, (int PlayerId, int Votes)> ComputeMvps(IEnumerable<(int MatchId, int PlayerId, int Votes)> voteCounts)
        {
            return voteCounts
                .GroupBy(v => v.MatchId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var top = g.OrderByDescending(v => v.Votes).ThenBy(v => v.PlayerId).First();
                        return (top.PlayerId, top.Votes);
                    });
        }

        private static double? RoundRating(double? value) =>
            value.HasValue ? Math.Round(value.Value, 2) : null;

        // ------------------------------------------------------------------
        // Yazma işlemleri
        // ------------------------------------------------------------------

        public int AddMatchWithCreator(Match match, int creatorPlayerId)
        {
            using var context = new MatcBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            match.CreatedByPlayerId = creatorPlayerId;
            if (match.CreatedAt == default) match.CreatedAt = DateTime.Now;
            context.Matchs.Add(match);
            context.SaveChanges();

            context.MatchParticipants.Add(new MatchParticipant
            {
                MatchId = match.MatchId,
                PlayerId = creatorPlayerId,
                Status = ParticipantStatus.Going,
                Side = ParticipantSide.Home,
                Confirmed = true,
                JoinedAt = DateTime.Now,
            });
            AddActivity(context, ActivityType.MatchCreated, creatorPlayerId, matchId: match.MatchId);
            context.SaveChanges();
            transaction.Commit();
            return match.MatchId;
        }

        public OperationResult<bool> CheckCanManage(int matchId, int playerId, bool isAdmin)
        {
            using var context = new MatcBuddyContext();
            var match = context.Matchs.AsNoTracking().FirstOrDefault(m => m.MatchId == matchId);
            if (match == null) return OperationResult<bool>.NotFound();
            return CanManage(context, match, playerId, isAdmin)
                ? OperationResult<bool>.Ok(true)
                : OperationResult<bool>.Forbidden();
        }

        public OperationResult<byte> Join(int matchId, int playerId, byte? side = null, int? slot = null)
        {
            using var context = new MatcBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var match = LockMatch(context, matchId);
            if (match == null) return OperationResult<byte>.NotFound();

            // side/slot: sahada dokunulan boş yer (doluysa normal kural)
            var result = ParticipationCore.Join(context, match, playerId, side is 1 or 2 ? side : null, checkReliability: true, preferredSlot: slot);
            if (!result.Success) return result;

            context.SaveChanges();
            transaction.Commit();
            return result;
        }

        /// <summary>
        /// Maçtan çekilme. "Katılıyor" iken maça az kala çekilmek güvenilirlik puanını düşürür (docs/reliability-api.md);
        /// mazeret kurucuya bildirilir, kurucu kabul ederse puan iade edilir. Yedek listeden çıkmak cezasızdır.
        /// </summary>
        public OperationResult<LeaveResultDto> Leave(int matchId, int playerId, string? reasonCode = null, string? note = null)
        {
            using var context = new MatcBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var match = LockMatch(context, matchId);
            if (match == null) return OperationResult<LeaveResultDto>.NotFound();

            var wasGoing = context.MatchParticipants.Any(p => p.MatchId == matchId && p.PlayerId == playerId && p.Status == ParticipantStatus.Going);
            var result = ParticipationCore.Leave(context, match, playerId);
            if (!result.Success) return OperationResult<LeaveResultDto>.Fail(result.Status, result.Code!, result.Args);

            var penalty = wasGoing ? ReliabilityCore.PenaltyFor(match, DateTime.Now) : new LeavePenalty("none", null, 0, 0);
            if (penalty.Type.HasValue && penalty.Penalty > 0)
            {
                ReliabilityCore.AddEvent(context, playerId, matchId, penalty.Type.Value, -penalty.Penalty, reasonCode, note);
            }
            // Kurucuya bildirim: maça yakın (ceza penceresinde) çekilmeler, mazeretiyle
            if (wasGoing && penalty.Type.HasValue && match.CreatedByPlayerId.HasValue && match.CreatedByPlayerId.Value != playerId)
            {
                var name = context.Players.Where(p => p.PlayerId == playerId)
                    .Select(p => (p.PlayerName + " " + p.PlayerSurname).Trim()).FirstOrDefault() ?? string.Empty;
                var text = string.IsNullOrEmpty(reasonCode)
                    ? Msg.Get(MessageKeys.Reliability.NotifWithdrewNoReason, name, match.MatchName ?? string.Empty)
                    : Msg.Get(MessageKeys.Reliability.NotifWithdrew, name, match.MatchName ?? string.Empty,
                        Msg.Get(MessageKeys.Reliability.ReasonPrefix + reasonCode.ToUpperInvariant()));
                AddNotification(context, NotificationType.MatchJoin, text, matchId, new[] { match.CreatedByPlayerId.Value });
            }

            context.SaveChanges();
            var score = ReliabilityCore.Recompute(context, playerId);
            context.SaveChanges();
            transaction.Commit();

            var applied = penalty.Type.HasValue ? penalty.Penalty : 0;
            return OperationResult<LeaveResultDto>.Ok(new LeaveResultDto
            {
                Message = applied > 0 ? Msg.Get(MessageKeys.Reliability.LeftWithPenalty, applied, score) : result.Message,
                Penalty = applied,
                NewScore = score,
            }, applied > 0 ? Msg.Get(MessageKeys.Reliability.LeftWithPenalty, applied, score) : result.Message);
        }

        public OperationResult<bool> Confirm(int matchId, int playerId)
        {
            using var context = new MatcBuddyContext();
            if (!context.Matchs.Any(m => m.MatchId == matchId)) return OperationResult<bool>.NotFound();

            var mine = context.MatchParticipants.FirstOrDefault(p => p.MatchId == matchId && p.PlayerId == playerId);
            if (mine == null || mine.Status != ParticipantStatus.Going)
                return OperationResult<bool>.BadRequest(ErrorCodes.Match.ConfirmOnlyGoing);

            mine.Confirmed = true;
            context.SaveChanges();
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Match.Confirmed));
        }

        public OperationResult<bool> SetSide(int matchId, int actorPlayerId, bool isAdmin, int targetPlayerId, byte side)
        {
            using var context = new MatcBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var match = LockMatch(context, matchId);
            if (match == null) return OperationResult<bool>.NotFound();
            if (!CanManage(context, match, actorPlayerId, isAdmin)) return OperationResult<bool>.Forbidden();

            var target = context.MatchParticipants.FirstOrDefault(p => p.MatchId == matchId && p.PlayerId == targetPlayerId);
            if (target == null || target.Status != ParticipantStatus.Going)
                return OperationResult<bool>.BadRequest(ErrorCodes.Match.PlayerNotInRoster);

            // Hedef tarafın kapasitesi: o taraftaki Going (oyuncu hariç) + misafir < ceil(UserCount / 2)
            if (target.Side != side)
            {
                var others = context.MatchParticipants
                    .Where(p => p.MatchId == matchId && p.Status == ParticipantStatus.Going && p.PlayerId != targetPlayerId)
                    .ToList();
                if (SideCount(others, LoadGuests(context, matchId), side) >= SideCapacity(match))
                    return OperationResult<bool>.BadRequest(SideFull);
            }

            if (target.Side != side) target.Slot = null; // taraf değişince eski yer geçersiz
            target.Side = side;
            context.SaveChanges();
            transaction.Commit();
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Match.SideUpdated));
        }

        /// <summary>
        /// Sahadaki dizilişi kaydeder (sürükle-bırak / yer değiştirme). Listede olmayan kadro kayıtlarına dokunulmaz.
        /// Taraf değişimi de buradan yapılabilir; her iki tarafın kapasitesi sonradan kontrol edilir.
        /// </summary>
        public OperationResult<bool> ArrangeLineup(int matchId, int actorPlayerId, bool isAdmin, List<LineupItemInput> items)
        {
            using var context = new MatcBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var match = LockMatch(context, matchId);
            if (match == null) return OperationResult<bool>.NotFound(ErrorCodes.Match.NotFound);
            // Diziliş kadrodaki tüm oyunculara açıktır (yalnızca kurucuya değil)
            if (!CanArrange(context, match, actorPlayerId, isAdmin)) return OperationResult<bool>.Forbidden();
            if (match.IsActive == RejectedStatus)
                return OperationResult<bool>.BadRequest(ErrorCodes.Match.RejectedRosterLocked);
            if (IsPast(match))
                return OperationResult<bool>.BadRequest(ErrorCodes.Match.PastRosterLocked);

            var going = context.MatchParticipants
                .Where(p => p.MatchId == matchId && p.Status == ParticipantStatus.Going)
                .ToList();
            var guests = LoadGuests(context, matchId);

            foreach (var item in items)
            {
                if (item.PlayerId.HasValue)
                {
                    var p = going.FirstOrDefault(x => x.PlayerId == item.PlayerId.Value);
                    if (p == null) return OperationResult<bool>.BadRequest(ErrorCodes.Match.PlayerNotInRoster);
                    p.Side = item.Side;
                    p.Slot = item.Slot;
                }
                else
                {
                    var g = guests.FirstOrDefault(x => x.GuestId == item.GuestId);
                    if (g == null) return OperationResult<bool>.NotFound(ErrorCodes.Match.GuestNotFound);
                    g.Side = item.Side;
                    g.Slot = item.Slot;
                }
            }

            var capacity = SideCapacity(match);
            if (SideCount(going, guests, ParticipantSide.Home) > capacity || SideCount(going, guests, ParticipantSide.Away) > capacity)
                return OperationResult<bool>.BadRequest(SideFull);

            context.SaveChanges();
            transaction.Commit();
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Match.LineupUpdated));
        }

        // ------------------------------------------------------------------
        // Misafir oyuncular (docs/match-participation-api.md "Ek: Misafir oyuncular")
        // ------------------------------------------------------------------

        // Yönetim + maç durumu kontrolü (kilitli satır üzerinde)
        private static OperationResult<T>? GuestGuard<T>(MatcBuddyContext context, Match match, int actorPlayerId, bool isAdmin)
        {
            if (!CanManage(context, match, actorPlayerId, isAdmin)) return OperationResult<T>.Forbidden();
            if (match.IsActive == RejectedStatus)
                return OperationResult<T>.BadRequest(ErrorCodes.Match.RejectedRosterLocked);
            if (IsPast(match))
                return OperationResult<T>.BadRequest(ErrorCodes.Match.PastRosterLocked);
            return null;
        }

        public OperationResult<int> AddGuest(int matchId, int actorPlayerId, bool isAdmin, byte side, string name, string? note)
        {
            using var context = new MatcBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var match = LockMatch(context, matchId);
            if (match == null) return OperationResult<int>.NotFound(ErrorCodes.Match.NotFound);
            var guard = GuestGuard<int>(context, match, actorPlayerId, isAdmin);
            if (guard != null) return guard;

            // Kapasite: Going + misafir < UserCount ve o taraftaki Going + misafir < ceil(UserCount / 2)
            var going = context.MatchParticipants
                .Where(p => p.MatchId == matchId && p.Status == ParticipantStatus.Going)
                .ToList();
            var guests = LoadGuests(context, matchId);
            if (going.Count + guests.Count >= match.UserCount || SideCount(going, guests, side) >= SideCapacity(match))
                return OperationResult<int>.BadRequest(SideFull);

            var guest = new MatchGuest
            {
                MatchId = matchId,
                Name = name,
                Note = note,
                Side = side,
                AddedByPlayerId = actorPlayerId,
                CreatedAt = DateTime.Now,
            };
            context.MatchGuests.Add(guest);
            context.SaveChanges();
            transaction.Commit();
            return OperationResult<int>.Ok(guest.GuestId, Msg.Get(MessageKeys.Match.GuestAdded));
        }

        public OperationResult<bool> UpdateGuest(int guestId, int actorPlayerId, bool isAdmin, string name, string? note, byte side)
        {
            using var context = new MatcBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var matchId = context.MatchGuests.AsNoTracking().Where(g => g.GuestId == guestId).Select(g => (int?)g.MatchId).FirstOrDefault();
            if (matchId == null) return OperationResult<bool>.NotFound(ErrorCodes.Match.GuestNotFound);
            var match = LockMatch(context, matchId.Value);
            if (match == null) return OperationResult<bool>.NotFound(ErrorCodes.Match.NotFound);
            var guard = GuestGuard<bool>(context, match, actorPlayerId, isAdmin);
            if (guard != null) return guard;

            var guests = LoadGuests(context, match.MatchId);
            var guest = guests.FirstOrDefault(g => g.GuestId == guestId);
            if (guest == null) return OperationResult<bool>.NotFound(ErrorCodes.Match.GuestNotFound);

            // Taraf değişiminde hedef tarafın kapasitesi (misafirin kendisi hariç)
            if (guest.Side != side)
            {
                var going = context.MatchParticipants
                    .Where(p => p.MatchId == match.MatchId && p.Status == ParticipantStatus.Going)
                    .ToList();
                if (SideCount(going, guests.Where(g => g.GuestId != guestId), side) >= SideCapacity(match))
                    return OperationResult<bool>.BadRequest(SideFull);
            }

            guest.Name = name;
            guest.Note = note;
            if (guest.Side != side) guest.Slot = null;
            guest.Side = side;
            context.SaveChanges();
            transaction.Commit();
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Match.GuestUpdated));
        }

        public OperationResult<bool> RemoveGuest(int guestId, int actorPlayerId, bool isAdmin)
        {
            using var context = new MatcBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var matchId = context.MatchGuests.AsNoTracking().Where(g => g.GuestId == guestId).Select(g => (int?)g.MatchId).FirstOrDefault();
            if (matchId == null) return OperationResult<bool>.NotFound(ErrorCodes.Match.GuestNotFound);
            var match = LockMatch(context, matchId.Value);
            if (match == null) return OperationResult<bool>.NotFound(ErrorCodes.Match.NotFound);
            var guard = GuestGuard<bool>(context, match, actorPlayerId, isAdmin);
            if (guard != null) return guard;

            var guests = LoadGuests(context, match.MatchId);
            var guest = guests.FirstOrDefault(g => g.GuestId == guestId);
            if (guest == null) return OperationResult<bool>.NotFound(ErrorCodes.Match.GuestNotFound);

            context.MatchGuests.Remove(guest);
            guests.Remove(guest);

            // Boşalan yere yedekteki ilk oyuncu yükselir, misafirin tarafına atanır (bildirim dahil)
            var participants = context.MatchParticipants.Where(p => p.MatchId == match.MatchId).ToList();
            PromoteFromWaitlist(context, match, participants, guest.Side, guests);

            context.SaveChanges();
            transaction.Commit();
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Match.GuestRemoved));
        }

        public OperationResult<bool> SubmitResult(int matchId, int actorPlayerId, bool isAdmin, int homeScore, int awayScore, List<PlayerStatInput> stats)
        {
            using var context = new MatcBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var match = LockMatch(context, matchId);
            if (match == null) return OperationResult<bool>.NotFound();
            if (!CanManage(context, match, actorPlayerId, isAdmin)) return OperationResult<bool>.Forbidden();
            if (!IsPast(match))
                return OperationResult<bool>.BadRequest(ErrorCodes.Match.ResultNotPlayed);

            var isFirstEntry = match.ResultEnteredAt == null;
            match.HomeScore = homeScore;
            match.AwayScore = awayScore;
            match.ResultEnteredAt = DateTime.Now;

            // Sonuç (ve istatistikler) her girişte bütünüyle yenilenir; Going olmayanlar yok sayılır
            var going = context.MatchParticipants
                .Where(p => p.MatchId == matchId && p.Status == ParticipantStatus.Going)
                .ToList();
            foreach (var participant in going)
            {
                var stat = stats.LastOrDefault(s => s.PlayerId == participant.PlayerId);
                participant.Goals = stat?.Goals ?? 0;
                participant.Assists = stat?.Assists ?? 0;
            }

            // Scored aktiviteleri: düzeltmede o maçın kayıtları yeniden yazılır
            var oldScored = context.Activities
                .Where(a => a.MatchId == matchId && a.Type == ActivityType.Scored)
                .ToList();
            context.Activities.RemoveRange(oldScored);
            foreach (var participant in going.Where(p => p.Goals > 0))
            {
                AddActivity(context, ActivityType.Scored, participant.PlayerId, matchId: matchId, value: participant.Goals);
            }

            // Bildirim sadece ilk girişte (düzeltmelerde tekrar gönderilmez)
            if (isFirstEntry)
            {
                AddNotification(context, NotificationType.MatchResult,
                    Msg.GetDefault(MessageKeys.Match.NotifResult, homeScore, awayScore, match.MatchName),
                    matchId, going.Select(p => p.PlayerId));
            }

            context.SaveChanges();
            transaction.Commit();
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Match.ResultSaved));
        }

        public OperationResult<bool> VoteMvp(int matchId, int voterPlayerId, int votedPlayerId)
        {
            using var context = new MatcBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var match = LockMatch(context, matchId);
            if (match == null) return OperationResult<bool>.NotFound();
            if (!IsPast(match))
                return OperationResult<bool>.BadRequest(ErrorCodes.Match.MvpNotOpen);
            if (voterPlayerId == votedPlayerId)
                return OperationResult<bool>.BadRequest(ErrorCodes.Match.MvpSelfVote);

            var goingIds = context.MatchParticipants
                .Where(p => p.MatchId == matchId && p.Status == ParticipantStatus.Going)
                .Select(p => p.PlayerId)
                .ToHashSet();
            if (!goingIds.Contains(voterPlayerId))
                return OperationResult<bool>.BadRequest(ErrorCodes.Match.MvpVoterNotPlayed);
            if (!goingIds.Contains(votedPlayerId))
                return OperationResult<bool>.BadRequest(ErrorCodes.Match.MvpVotedNotPlayed);

            var vote = context.MvpVotes.FirstOrDefault(v => v.MatchId == matchId && v.VoterPlayerId == voterPlayerId);
            if (vote == null)
            {
                context.MvpVotes.Add(new MvpVote
                {
                    MatchId = matchId,
                    VoterPlayerId = voterPlayerId,
                    VotedPlayerId = votedPlayerId,
                    CreatedAt = DateTime.Now
                });
            }
            else
            {
                vote.VotedPlayerId = votedPlayerId;
                vote.CreatedAt = DateTime.Now;
            }
            context.SaveChanges();

            UpsertMvpActivity(context, matchId);
            context.SaveChanges();
            transaction.Commit();
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Match.MvpVoted));
        }

        public OperationResult<bool> RatePlayers(int matchId, int raterPlayerId, List<PlayerRatingInput> ratings)
        {
            using var context = new MatcBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var match = LockMatch(context, matchId);
            if (match == null) return OperationResult<bool>.NotFound();
            if (!IsPast(match))
                return OperationResult<bool>.BadRequest(ErrorCodes.Match.RatingNotOpen);

            var goingIds = context.MatchParticipants
                .Where(p => p.MatchId == matchId && p.Status == ParticipantStatus.Going)
                .Select(p => p.PlayerId)
                .ToHashSet();
            if (!goingIds.Contains(raterPlayerId))
                return OperationResult<bool>.BadRequest(ErrorCodes.Match.RaterNotPlayed);

            // Aynı oyuncu birden fazla gönderildiyse sonuncusu geçerli
            var distinctRatings = ratings
                .GroupBy(r => r.PlayerId)
                .Select(g => g.Last())
                .ToList();

            if (distinctRatings.Any(r => r.PlayerId == raterPlayerId))
                return OperationResult<bool>.BadRequest(ErrorCodes.Match.RatingSelf);
            if (distinctRatings.Any(r => !goingIds.Contains(r.PlayerId)))
                return OperationResult<bool>.BadRequest(ErrorCodes.Match.RatedNotPlayed);

            var ratedIds = distinctRatings.Select(r => r.PlayerId).ToList();
            var existing = context.PlayerRatings
                .Where(r => r.MatchId == matchId && r.RaterPlayerId == raterPlayerId && ratedIds.Contains(r.RatedPlayerId))
                .ToList();

            var now = DateTime.Now;
            foreach (var input in distinctRatings)
            {
                var row = existing.FirstOrDefault(r => r.RatedPlayerId == input.PlayerId);
                if (row == null)
                {
                    context.PlayerRatings.Add(new PlayerRating
                    {
                        MatchId = matchId,
                        RaterPlayerId = raterPlayerId,
                        RatedPlayerId = input.PlayerId,
                        Score = (byte)input.Score,
                        CreatedAt = now
                    });
                }
                else
                {
                    row.Score = (byte)input.Score;
                    row.CreatedAt = now;
                }
            }
            context.SaveChanges();

            // UserScore = round(ortalama(Score) * 100 / match.ratingMax)  (0–100; varsayılan ratingMax 5 → × 20)
            var averages = context.PlayerRatings
                .Where(r => ratedIds.Contains(r.RatedPlayerId))
                .GroupBy(r => r.RatedPlayerId)
                .Select(g => new { PlayerId = g.Key, Avg = g.Average(r => (double)r.Score) })
                .ToList();
            var players = context.Players.Where(p => ratedIds.Contains(p.PlayerId)).ToList();
            foreach (var player in players)
            {
                var avg = averages.FirstOrDefault(a => a.PlayerId == player.PlayerId)?.Avg;
                if (avg.HasValue)
                {
                    player.UserScore = (int)Math.Round(avg.Value * 100.0 / P.Int(ParameterKeys.Match.RatingMax), MidpointRounding.AwayFromZero);
                }
            }

            context.SaveChanges();
            transaction.Commit();
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Match.RatingsSaved));
        }

        public OperationResult<ToggleLikeResultDto> ToggleLike(int matchId, int playerId, bool likeOnly)
        {
            using var context = new MatcBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var match = LockMatch(context, matchId);
            if (match == null) return OperationResult<ToggleLikeResultDto>.NotFound();

            var like = context.MatchLikes.FirstOrDefault(l => l.MatchId == matchId && l.PlayerId == playerId);
            bool liked;
            if (like != null)
            {
                if (likeOnly)
                {
                    // Eski MatchLiked: zaten beğenilmişse değişiklik yok
                    transaction.Commit();
                    return OperationResult<ToggleLikeResultDto>.Ok(new ToggleLikeResultDto { Liked = true, Likes = match.Likes }, Msg.Get(MessageKeys.Match.AlreadyLiked));
                }
                context.MatchLikes.Remove(like);
                match.Likes = Math.Max(0, match.Likes - 1);
                liked = false;
            }
            else
            {
                context.MatchLikes.Add(new MatchLike { MatchId = matchId, PlayerId = playerId, CreatedAt = DateTime.Now });
                match.Likes += 1;
                liked = true;
            }

            context.SaveChanges();
            transaction.Commit();
            return OperationResult<ToggleLikeResultDto>.Ok(new ToggleLikeResultDto { Liked = liked, Likes = match.Likes },
                Msg.Get(liked ? MessageKeys.Match.Liked : MessageKeys.Match.Unliked));
        }

        /// <summary>
        /// Maç bilgilerini düzenler: ad, açıklama, kadro sayısı (yönetici). Admin ayrıca tarih / onay durumu değiştirebilir.
        /// Rezervasyon alanları (saha, süre, fiyat) korunur: saat değişikliği yeni rezervasyon gerektirir.
        /// Kadro sayısı kadronun (Going + misafir) altına inemez; artarsa yedekler asile yükselir.
        /// Değişiklik kadrodaki (ve yedekteki) diğer oyunculara bildirilir.
        /// </summary>
        public OperationResult<bool> UpdateInfo(int actorPlayerId, bool isAdmin, UpdateMatchInfoInput input)
        {
            using var context = new MatcBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var match = LockMatch(context, input.MatchId);
            if (match == null) return OperationResult<bool>.NotFound(ErrorCodes.Match.NotFound);
            if (!CanManage(context, match, actorPlayerId, isAdmin)) return OperationResult<bool>.Forbidden();
            if (!isAdmin && (IsPast(match) || match.IsActive == RejectedStatus))
                return OperationResult<bool>.BadRequest(ErrorCodes.Match.EditClosed);

            var changes = new List<string>();

            if (input.MatchName != null)
            {
                var name = input.MatchName.Trim();
                if (name.Length < 2 || name.Length > 100)
                    return OperationResult<bool>.FieldError("matchName", ErrorCodes.Match.NameInvalid);
                if (name != match.MatchName)
                {
                    match.MatchName = name;
                    changes.Add("name");
                }
            }

            if (input.Description != null)
            {
                // Sütun boş bırakılamaz (NOT NULL): açıklama silinirse boş metin yazılır
                var desc = string.IsNullOrWhiteSpace(input.Description) ? string.Empty : input.Description.Trim();
                if (desc != null && desc.Length > 1000)
                    return OperationResult<bool>.FieldError("description", ErrorCodes.Validation.TooLong, "description", 1000);
                if (desc != match.Description)
                {
                    match.Description = desc;
                    changes.Add("description");
                }
            }

            var oldCount = match.UserCount;
            if (input.UserCount is > 0 && input.UserCount.Value != match.UserCount)
            {
                var playersPerSide = match.PitchId.HasValue
                    ? context.Pitches.AsNoTracking().Where(p => p.PitchId == match.PitchId.Value).Select(p => (int?)p.PlayersPerSide).FirstOrDefault()
                    : null;
                var min = VenuePricing.MinUserCount();
                var max = VenuePricing.MaxUserCount(playersPerSide ?? P.Int(ParameterKeys.Venue.PlayersPerSideMax));
                if (input.UserCount.Value < min || input.UserCount.Value > max)
                    return OperationResult<bool>.FieldError("userCount", ErrorCodes.Validation.OutOfRange,
                        Msg.Get(MessageKeys.Reservation.LabelUserCount), min, max);

                var going = context.MatchParticipants
                    .Where(p => p.MatchId == match.MatchId && p.Status == ParticipantStatus.Going)
                    .ToList();
                var guests = LoadGuests(context, match.MatchId);
                var roster = going.Count + guests.Count;
                if (input.UserCount.Value < roster)
                    return OperationResult<bool>.FieldError("userCount", ErrorCodes.Match.CapacityBelowRoster, roster);

                // Yeni taraf kapasitesine sığmayan taraftan son gelenler diğer tarafa kayar (toplam zaten sığıyor)
                match.UserCount = input.UserCount.Value;
                var sideCap = SideCapacity(match);
                foreach (var side in new[] { ParticipantSide.Home, ParticipantSide.Away })
                {
                    var other = side == ParticipantSide.Home ? ParticipantSide.Away : ParticipantSide.Home;
                    var overflow = SideCount(going, guests, side) - sideCap;
                    if (overflow <= 0) continue;
                    foreach (var g in guests.Where(g => g.Side == side).OrderByDescending(g => g.GuestId).Take(overflow).ToList())
                    {
                        g.Side = other;
                        g.Slot = null;
                        overflow--;
                    }
                    foreach (var p in going.Where(p => p.Side == side).OrderByDescending(p => p.JoinedAt).Take(Math.Max(overflow, 0)).ToList())
                    {
                        p.Side = other;
                        p.Slot = null;
                    }
                }
                changes.Add("capacity");
            }

            if (isAdmin)
            {
                if (input.MatchDate.HasValue && input.MatchDate != match.MatchDate)
                {
                    match.MatchDate = input.MatchDate;
                    changes.Add("date");
                }
                if (input.IsActive is >= 1 and <= 3) match.IsActive = input.IsActive.Value;
            }

            if (changes.Count == 0)
            {
                context.SaveChanges();
                transaction.Commit();
                return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Match.InfoUpdated));
            }

            context.SaveChanges();

            // Kapasite arttıysa yedekler asile yükselir
            if (match.UserCount > oldCount)
            {
                var participants = context.MatchParticipants.Where(p => p.MatchId == match.MatchId).ToList();
                PromoteFromWaitlist(context, match, participants, null);
            }

            // Kadrodaki ve yedekteki diğer oyunculara haber ver
            var recipients = context.MatchParticipants.AsNoTracking()
                .Where(p => p.MatchId == match.MatchId && p.PlayerId != actorPlayerId
                            && (p.Status == ParticipantStatus.Going || p.Status == ParticipantStatus.Waitlist))
                .Select(p => p.PlayerId)
                .ToList();
            if (recipients.Count > 0)
            {
                var parts = changes.Select(c => c switch
                {
                    "capacity" => Msg.GetDefault(MessageKeys.Match.NotifCapacityChanged, oldCount, match.UserCount),
                    "name" => Msg.GetDefault(MessageKeys.Match.LabelFieldName),
                    "description" => Msg.GetDefault(MessageKeys.Match.LabelFieldDescription),
                    _ => Msg.GetDefault(MessageKeys.Match.LabelFieldDate),
                });
                AddNotification(context, NotificationType.MatchUpdated,
                    Msg.GetDefault(MessageKeys.Match.NotifUpdated, match.MatchName ?? string.Empty, string.Join(", ", parts)),
                    match.MatchId, recipients);
            }

            context.SaveChanges();
            transaction.Commit();
            return OperationResult<bool>.Ok(true, Msg.Get(MessageKeys.Match.InfoUpdated));
        }

        public void FillFromWaitlist(int matchId)
        {
            using var context = new MatcBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var match = LockMatch(context, matchId);
            if (match == null) return;

            var participants = context.MatchParticipants.Where(p => p.MatchId == matchId).ToList();
            PromoteFromWaitlist(context, match, participants, null);

            context.SaveChanges();
            transaction.Commit();
        }

        // MvpWon aktivitesi güncel lidere göre (en çok oy, beraberlikte en düşük playerId) upsert edilir; oy yoksa silinir
        private static void UpsertMvpActivity(MatcBuddyContext context, int matchId)
        {
            var leader = context.MvpVotes
                .Where(v => v.MatchId == matchId)
                .GroupBy(v => v.VotedPlayerId)
                .Select(g => new { PlayerId = g.Key, Votes = g.Count() })
                .ToList()
                .OrderByDescending(x => x.Votes).ThenBy(x => x.PlayerId)
                .FirstOrDefault();

            var existing = context.Activities
                .Where(a => a.MatchId == matchId && a.Type == ActivityType.MvpWon)
                .ToList();

            if (leader == null)
            {
                context.Activities.RemoveRange(existing);
                return;
            }

            var row = existing.FirstOrDefault();
            context.Activities.RemoveRange(existing.Skip(1));
            if (row == null)
            {
                AddActivity(context, ActivityType.MvpWon, leader.PlayerId, matchId: matchId, value: leader.Votes);
            }
            else
            {
                if (row.ActorPlayerId != leader.PlayerId)
                {
                    row.ActorPlayerId = leader.PlayerId;
                    row.CreatedAt = DateTime.Now;
                }
                row.Value = leader.Votes;
            }
        }

        // ------------------------------------------------------------------
        // Okuma işlemleri
        // ------------------------------------------------------------------

        public List<MatchFeedItemDto> GetMatchFeed(int playerId)
        {
            using var context = new MatcBuddyContext();

            var matches = (from m in context.Matchs.AsNoTracking()
                           join s in context.Stadiums.AsNoTracking() on m.StadiumId equals s.StadiumId
                           orderby m.MatchDate descending
                           select new
                           {
                               Entity = m,
                               m.MatchId,
                               m.MatchName,
                               m.MatchDate,
                               m.Description,
                               m.IsActive,
                               m.UserCount,
                               m.Likes,
                               m.StadiumId,
                               s.StadiumName,
                               StadiumAddress = s.Address,
                               m.CreatedByPlayerId,
                               m.HomeScore,
                               m.AwayScore
                           }).ToList();

            // Tesis paketi: saha / kapak / takım adları
            var venueLookup = VenueRules.MatchVenueLookup.Load(context, matches.Select(m => m.Entity));

            // Aktif kadro kayıtları + benim (gelemiyor dahil) kayıtlarım
            var participants = context.MatchParticipants.AsNoTracking()
                .Where(p => p.Status != ParticipantStatus.Declined || p.PlayerId == playerId)
                .Select(p => new { p.MatchParticipantId, p.MatchId, p.PlayerId, p.Status, p.JoinedAt })
                .ToList();
            var participantsByMatch = participants.ToLookup(p => p.MatchId);

            // Sosyal: takip ettiklerim ve engeller (iki yön)
            var graph = SocialGraph.Load(context, playerId);

            // Misafir sayıları (kadroya / doluluğa sayılır)
            var guestCounts = GuestCounts(context, null);

            var likedMatchIds = context.MatchLikes.AsNoTracking()
                .Where(l => l.PlayerId == playerId)
                .Select(l => l.MatchId)
                .ToHashSet();

            var voteCounts = context.MvpVotes.AsNoTracking()
                .GroupBy(v => new { v.MatchId, v.VotedPlayerId })
                .Select(g => new { g.Key.MatchId, g.Key.VotedPlayerId, Votes = g.Count() })
                .ToList();
            var mvps = ComputeMvps(voteCounts.Select(v => (v.MatchId, v.VotedPlayerId, v.Votes)));

            // Önizleme: Going oyuncuların ilk 5'i (katılım sırasına göre); engellediklerim/beni engelleyenler hariç
            var previews = participantsByMatch.ToDictionary(
                g => g.Key,
                g => g.Where(p => p.Status == ParticipantStatus.Going && !graph.IsBlocked(p.PlayerId))
                      .OrderBy(p => p.JoinedAt).ThenBy(p => p.MatchParticipantId)
                      .Take(5).Select(p => p.PlayerId).ToList());

            // Takip ettiklerimden Going olanlar (sayı + ilk 3)
            var followingGoing = participantsByMatch.ToDictionary(
                g => g.Key,
                g => g.Where(p => p.Status == ParticipantStatus.Going && graph.IFollow(p.PlayerId))
                      .OrderBy(p => p.JoinedAt).ThenBy(p => p.MatchParticipantId)
                      .Select(p => p.PlayerId).ToList());

            var neededPlayerIds = previews.Values.SelectMany(x => x)
                .Concat(followingGoing.Values.SelectMany(x => x.Take(3)))
                .Concat(matches.Where(m => m.CreatedByPlayerId.HasValue).Select(m => m.CreatedByPlayerId!.Value))
                .Concat(mvps.Values.Select(v => v.PlayerId))
                .Distinct()
                .ToList();
            var players = context.Players.AsNoTracking()
                .Where(p => neededPlayerIds.Contains(p.PlayerId))
                .ToList()
                .ToDictionary(p => p.PlayerId, PlayerSummaryDto.FromPlayer);

            PlayerSummaryDto? PlayerOrNull(int? id) =>
                id.HasValue && players.TryGetValue(id.Value, out var dto) ? dto : null;

            return matches.Select(m =>
            {
                var rows = participantsByMatch[m.MatchId].ToList();
                var mine = rows.FirstOrDefault(p => p.PlayerId == playerId);
                var guestCount = guestCounts.TryGetValue(m.MatchId, out var gc) ? gc : 0;
                var item = new MatchFeedItemDto
                {
                    MatchId = m.MatchId,
                    MatchName = m.MatchName,
                    MatchDate = m.MatchDate,
                    Description = m.Description,
                    IsActive = m.IsActive,
                    Capacity = m.UserCount,
                    GoingCount = rows.Count(p => p.Status == ParticipantStatus.Going) + guestCount,
                    GuestCount = guestCount,
                    WaitlistCount = rows.Count(p => p.Status == ParticipantStatus.Waitlist),
                    Likes = m.Likes,
                    LikedByMe = likedMatchIds.Contains(m.MatchId),
                    MyStatus = mine?.Status,
                    StadiumId = m.StadiumId,
                    StadiumName = m.StadiumName,
                    StadiumAddress = m.StadiumAddress,
                    CreatedBy = PlayerOrNull(m.CreatedByPlayerId),
                    ParticipantsPreview = previews.TryGetValue(m.MatchId, out var ids)
                        ? ids.Select(id => PlayerOrNull(id)).Where(p => p != null).Select(p => p!).ToList()
                        : new List<PlayerSummaryDto>(),
                    HomeScore = m.HomeScore,
                    AwayScore = m.AwayScore,
                    Mvp = mvps.TryGetValue(m.MatchId, out var mvp) ? PlayerOrNull(mvp.PlayerId) : null,
                    FollowingGoingCount = followingGoing.TryGetValue(m.MatchId, out var fg) ? fg.Count : 0,
                    FollowingPreview = fg == null
                        ? new List<PlayerSummaryDto>()
                        : fg.Take(3).Select(id => PlayerOrNull(id)).Where(p => p != null).Select(p => p!).ToList(),
                };
                venueLookup.Fill(item, m.Entity);
                return item;
            }).ToList();
        }

        public MatchDetailDto? GetMatchDetail(int matchId, int playerId, bool isAdmin)
        {
            using var context = new MatcBuddyContext();

            var match = context.Matchs.AsNoTracking().FirstOrDefault(m => m.MatchId == matchId);
            if (match == null) return null;

            var stadium = context.Stadiums.AsNoTracking().FirstOrDefault(s => s.StadiumId == match.StadiumId);

            var participants = context.MatchParticipants.AsNoTracking()
                .Include(p => p.Player)
                .Where(p => p.MatchId == matchId)
                .ToList();
            var mine = participants.FirstOrDefault(p => p.PlayerId == playerId);

            var guests = context.MatchGuests.AsNoTracking()
                .Include(g => g.AddedByPlayer)
                .Where(g => g.MatchId == matchId)
                .OrderBy(g => g.CreatedAt).ThenBy(g => g.GuestId)
                .ToList();

            var myFollowing = context.Follows.AsNoTracking()
                .Where(f => f.FollowerId == playerId)
                .Select(f => f.FolloweeId)
                .ToHashSet();

            var voteCounts = context.MvpVotes.AsNoTracking()
                .Where(v => v.MatchId == matchId)
                .GroupBy(v => v.VotedPlayerId)
                .Select(g => new { PlayerId = g.Key, Votes = g.Count() })
                .ToList()
                .ToDictionary(x => x.PlayerId, x => x.Votes);

            var avgRatings = context.PlayerRatings.AsNoTracking()
                .Where(r => r.MatchId == matchId)
                .GroupBy(r => r.RatedPlayerId)
                .Select(g => new { PlayerId = g.Key, Avg = g.Average(r => (double)r.Score) })
                .ToList()
                .ToDictionary(x => x.PlayerId, x => x.Avg);

            var myVote = context.MvpVotes.AsNoTracking()
                .Where(v => v.MatchId == matchId && v.VoterPlayerId == playerId)
                .Select(v => (int?)v.VotedPlayerId)
                .FirstOrDefault();

            var myRatings = context.PlayerRatings.AsNoTracking()
                .Where(r => r.MatchId == matchId && r.RaterPlayerId == playerId)
                .Select(r => new MyRatingDto { PlayerId = r.RatedPlayerId, Score = r.Score })
                .ToList();

            Player? createdBy = match.CreatedByPlayerId.HasValue
                ? (participants.FirstOrDefault(p => p.PlayerId == match.CreatedByPlayerId.Value)?.Player
                   ?? context.Players.AsNoTracking().FirstOrDefault(p => p.PlayerId == match.CreatedByPlayerId.Value))
                : null;

            MatchMvpDto? mvp = null;
            if (voteCounts.Count > 0)
            {
                var top = voteCounts.OrderByDescending(v => v.Value).ThenBy(v => v.Key).First();
                var mvpPlayer = participants.FirstOrDefault(p => p.PlayerId == top.Key)?.Player
                                ?? context.Players.AsNoTracking().FirstOrDefault(p => p.PlayerId == top.Key);
                if (mvpPlayer != null)
                {
                    mvp = new MatchMvpDto { Player = PlayerSummaryDto.FromPlayer(mvpPlayer), Votes = top.Value };
                }
            }

            // Tesis paketi: işletme, tesis yorumu
            var venueLookup = VenueRules.MatchVenueLookup.Load(context, new[] { match });
            int? ownerPlayerId = stadium?.OwnerPlayerId;
            var myReview = context.VenueReviews.AsNoTracking()
                .Where(r => r.MatchId == matchId && r.PlayerId == playerId)
                .Select(r => new MyReviewDto { Rating = r.Rating, Comment = r.Comment })
                .FirstOrDefault();
            var canReview = match.PitchId.HasValue
                            && match.IsActive != RejectedStatus
                            && match.MatchDate.HasValue
                            && match.MatchDate.Value.AddMinutes(match.DurationMinutes) <= DateTime.Now
                            && mine?.Status == ParticipantStatus.Going;

            // Güvenilirlik rozetleri ve "gelmedi" işaretleri (docs/reliability-api.md)
            var canManage = CanManage(context, match, playerId, isAdmin);
            var badges = ReliabilityCore.Badges(context, participants.Select(p => p.PlayerId));
            var noShows = context.ReliabilityEvents.AsNoTracking()
                .Where(e => e.MatchId == match.MatchId && e.Type == ReliabilityEventType.NoShow)
                .Select(e => e.PlayerId).ToHashSet();

            var phones = VisiblePhones(context, match, participants, playerId, mine?.Status);

            var detail = new MatchDetailDto
            {
                OwnerPlayerId = ownerPlayerId,
                CanReview = canReview,
                MyReview = myReview,
                MatchId = match.MatchId,
                MatchName = match.MatchName,
                MatchDate = match.MatchDate,
                Description = match.Description,
                IsActive = match.IsActive,
                Capacity = match.UserCount,
                Likes = match.Likes,
                LikedByMe = context.MatchLikes.Any(l => l.MatchId == matchId && l.PlayerId == playerId),
                Stadium = stadium == null ? null : new MatchStadiumDto
                {
                    StadiumId = stadium.StadiumId,
                    StadiumName = stadium.StadiumName,
                    Address = stadium.Address,
                    Location = stadium.Location,
                    OpeningTime = stadium.OpeningTime?.ToString("HH:mm:ss"),
                    ClosingTime = stadium.ClosingTime?.ToString("HH:mm:ss"),
                },
                CreatedBy = createdBy == null ? null : PlayerSummaryDto.FromPlayer(createdBy),
                CanManage = canManage,
                CanArrange = canManage || mine?.Status == ParticipantStatus.Going,
                IsPast = IsPast(match),
                MyStatus = mine?.Status,
                MyConfirmed = mine?.Confirmed ?? false,
                MySide = mine?.Side,
                // Going önce, sonra yedekler; her grup katılım sırasına göre
                Participants = participants
                    .Where(p => p.Status == ParticipantStatus.Going || p.Status == ParticipantStatus.Waitlist)
                    .OrderBy(p => p.Status)
                    .ThenBy(p => p.JoinedAt)
                    .ThenBy(p => p.MatchParticipantId)
                    .Select(p => new MatchParticipantDto
                    {
                        Player = PlayerSummaryDto.FromPlayer(p.Player),
                        Status = p.Status,
                        Side = p.Side,
                        Slot = p.Slot,
                        Confirmed = p.Confirmed,
                        JoinedAt = p.JoinedAt,
                        Goals = p.Goals,
                        Assists = p.Assists,
                        MvpVotes = voteCounts.TryGetValue(p.PlayerId, out var votes) ? votes : 0,
                        AvgRating = avgRatings.TryGetValue(p.PlayerId, out var avg) ? RoundRating(avg) : null,
                        IFollow = myFollowing.Contains(p.PlayerId),
                        Reliability = badges.TryGetValue(p.PlayerId, out var b)
                            ? new ReliabilityBadgeDto { Score = b.Score, Level = b.Level, CompletedMatches = b.CompletedMatches }
                            : null,
                        NoShow = noShows.Contains(p.PlayerId),
                        Phone = phones.TryGetValue(p.PlayerId, out var phone) ? phone : null,
                    })
                    .ToList(),
                Guests = guests.Select(g => new MatchGuestDto
                {
                    GuestId = g.GuestId,
                    Name = g.Name,
                    Note = g.Note,
                    Side = g.Side,
                    Slot = g.Slot,
                    AddedBy = g.AddedByPlayer == null ? null : PlayerSummaryDto.FromPlayer(g.AddedByPlayer),
                    CreatedAt = g.CreatedAt,
                }).ToList(),
                Result = match.HomeScore.HasValue && match.AwayScore.HasValue
                    ? new MatchResultDto { HomeScore = match.HomeScore.Value, AwayScore = match.AwayScore.Value, EnteredAt = match.ResultEnteredAt }
                    : null,
                Mvp = mvp,
                MyMvpVote = myVote,
                MyRatings = myRatings,
            };
            venueLookup.Fill(detail, match);

            detail.MinReliability = match.MinReliability;
            detail.RepeatWeekly = MatchSeriesService.IsActive(context, match.SeriesId);
            detail.NoShowWindowOpen = canManage && IsPast(match) && match.ReliabilitySettledAt == null && match.IsActive != RejectedStatus
                && match.MatchDate!.Value.AddHours(P.Int(ParameterKeys.Reliability.NoShowMarkWindowHours)) > DateTime.Now;
            if (canManage) detail.Withdrawals = new EFReliabilityDal().Withdrawals(match.MatchId);
            return detail;
        }

        /// <summary>
        /// Görüntüleyenin arayabileceği oyuncuların numaraları (oyuncunun PhoneVisibility ayarına göre):
        /// "maç yöneticileri" → yalnızca maçın kurucusu; "maçtaki herkes" → kurucu + kadrodaki (Going) oyuncular.
        /// Site yöneticisi olmak numara görmeyi sağlamaz. Reddedilmiş maçta numara verilmez.
        /// </summary>
        private static Dictionary<int, string> VisiblePhones(MatcBuddyContext context, Match match, List<MatchParticipant> participants,
            int viewerId, byte? viewerStatus)
        {
            var result = new Dictionary<int, string>();
            if (match.IsActive == RejectedStatus) return result;
            var viewerIsManager = CanManage(context, match, viewerId, isAdmin: false);
            var viewerIsPlayer = viewerStatus == ParticipantStatus.Going;
            if (!viewerIsManager && !viewerIsPlayer) return result;

            var targets = participants
                .Where(p => p.PlayerId != viewerId
                            && (p.Status == ParticipantStatus.Going || p.Status == ParticipantStatus.Waitlist)
                            && !string.IsNullOrWhiteSpace(p.Player?.PhoneNumber))
                .ToList();
            if (targets.Count == 0) return result;
            var ids = targets.Select(p => p.PlayerId).ToList();
            var settings = context.PlayerSettings.AsNoTracking()
                .Where(s => ids.Contains(s.PlayerId))
                .ToDictionary(s => s.PlayerId, s => s.PhoneVisibility);

            foreach (var p in targets)
            {
                var visibility = settings.TryGetValue(p.PlayerId, out var v) ? v : PhoneVisibilities.MatchManagers;
                var allowed = visibility switch
                {
                    PhoneVisibilities.MatchManagers => viewerIsManager,
                    PhoneVisibilities.MatchPlayers => viewerIsManager || viewerIsPlayer,
                    _ => false,
                };
                if (allowed) result[p.PlayerId] = p.Player!.PhoneNumber!.Trim();
            }
            return result;
        }

        public PlayerStatsDto? GetPlayerStats(int playerId)
        {
            using var context = new MatcBuddyContext();

            var player = context.Players.AsNoTracking().FirstOrDefault(p => p.PlayerId == playerId);
            if (player == null) return null;

            var now = DateTime.Now;
            var played = (from p in context.MatchParticipants.AsNoTracking()
                          join m in context.Matchs.AsNoTracking() on p.MatchId equals m.MatchId
                          where p.PlayerId == playerId && p.Status == ParticipantStatus.Going && m.MatchDate < now
                          select new
                          {
                              m.MatchId,
                              m.MatchName,
                              m.MatchDate,
                              m.HomeScore,
                              m.AwayScore,
                              p.Side,
                              p.Goals,
                              p.Assists
                          }).ToList();

            // Oyuncunun oy aldığı geçmiş maçlarda MVP hesabı
            var votedMatchIds = context.MvpVotes.AsNoTracking()
                .Where(v => v.VotedPlayerId == playerId)
                .Select(v => v.MatchId)
                .Distinct()
                .ToList();
            var voteCounts = (from v in context.MvpVotes.AsNoTracking()
                              join m in context.Matchs.AsNoTracking() on v.MatchId equals m.MatchId
                              where votedMatchIds.Contains(v.MatchId) && m.MatchDate < now
                              group v by new { v.MatchId, v.VotedPlayerId } into g
                              select new { g.Key.MatchId, g.Key.VotedPlayerId, Votes = g.Count() }).ToList();
            var mvps = ComputeMvps(voteCounts.Select(v => (v.MatchId, v.VotedPlayerId, v.Votes)));
            var mvpMatchIds = mvps.Where(x => x.Value.PlayerId == playerId).Select(x => x.Key).ToHashSet();

            var ratingScores = context.PlayerRatings.AsNoTracking()
                .Where(r => r.RatedPlayerId == playerId)
                .Select(r => (double)r.Score)
                .ToList();

            return new PlayerStatsDto
            {
                PlayerId = playerId,
                MatchesPlayed = played.Count,
                Goals = played.Sum(x => x.Goals),
                Assists = played.Sum(x => x.Assists),
                MvpCount = mvpMatchIds.Count,
                AvgRating = ratingScores.Count > 0 ? RoundRating(ratingScores.Average()) : null,
                RatingCount = ratingScores.Count,
                UserScore = player.UserScore,
                RecentMatches = played
                    .OrderByDescending(x => x.MatchDate)
                    .Take(5)
                    .Select(x => new PlayerRecentMatchDto
                    {
                        MatchId = x.MatchId,
                        MatchName = x.MatchName,
                        MatchDate = x.MatchDate,
                        HomeScore = x.HomeScore,
                        AwayScore = x.AwayScore,
                        Side = x.Side,
                        Goals = x.Goals,
                        Assists = x.Assists,
                        WasMvp = mvpMatchIds.Contains(x.MatchId),
                    })
                    .ToList(),
            };
        }

        public List<LeaderboardRowDto> GetLeaderboard(int top, int viewerPlayerId)
        {
            using var context = new MatcBuddyContext();
            var now = DateTime.Now;

            // Admin hesaplar, silinmiş hesaplar ve engeller (iki yön) hariç
            var blocked = SocialGraph.Load(context, viewerPlayerId).BlockedEither;
            var players = context.Players.AsNoTracking()
                .Where(p => p.IsAdmin == 0 && p.Role == PlayerRoles.Player && !p.IsDeleted)
                .ToList()
                .Where(p => !blocked.Contains(p.PlayerId))
                .ToList();

            var played = (from p in context.MatchParticipants.AsNoTracking()
                          join m in context.Matchs.AsNoTracking() on p.MatchId equals m.MatchId
                          where p.Status == ParticipantStatus.Going && m.MatchDate < now
                          group p by p.PlayerId into g
                          select new
                          {
                              PlayerId = g.Key,
                              Count = g.Count(),
                              Goals = g.Sum(x => x.Goals),
                              Assists = g.Sum(x => x.Assists)
                          }).ToList().ToDictionary(x => x.PlayerId);

            var ratings = context.PlayerRatings.AsNoTracking()
                .GroupBy(r => r.RatedPlayerId)
                .Select(g => new { PlayerId = g.Key, Avg = g.Average(r => (double)r.Score) })
                .ToList()
                .ToDictionary(x => x.PlayerId, x => x.Avg);

            var voteCounts = (from v in context.MvpVotes.AsNoTracking()
                              join m in context.Matchs.AsNoTracking() on v.MatchId equals m.MatchId
                              where m.MatchDate < now
                              group v by new { v.MatchId, v.VotedPlayerId } into g
                              select new { g.Key.MatchId, g.Key.VotedPlayerId, Votes = g.Count() }).ToList();
            var mvpCounts = ComputeMvps(voteCounts.Select(v => (v.MatchId, v.VotedPlayerId, v.Votes)))
                .Values
                .GroupBy(x => x.PlayerId)
                .ToDictionary(g => g.Key, g => g.Count());

            return players
                .Select(p =>
                {
                    played.TryGetValue(p.PlayerId, out var stat);
                    return new LeaderboardRowDto
                    {
                        Player = PlayerSummaryDto.FromPlayer(p),
                        MatchesPlayed = stat?.Count ?? 0,
                        Goals = stat?.Goals ?? 0,
                        Assists = stat?.Assists ?? 0,
                        MvpCount = mvpCounts.TryGetValue(p.PlayerId, out var mc) ? mc : 0,
                        AvgRating = ratings.TryGetValue(p.PlayerId, out var avg) ? RoundRating(avg) : null,
                        UserScore = p.UserScore,
                    };
                })
                .OrderByDescending(r => r.UserScore)
                .ThenByDescending(r => r.MatchesPlayed)
                .ThenBy(r => r.Player.PlayerId)
                .Take(top)
                .ToList();
        }

        public List<CalendarItemDto> GetMyCalendar(int playerId, DateTime fromDate, DateTime toDate)
        {
            using var context = new MatcBuddyContext();

            var mine = context.MatchParticipants.AsNoTracking()
                .Where(p => p.PlayerId == playerId
                            && (p.Status == ParticipantStatus.Going || p.Status == ParticipantStatus.Waitlist))
                .Select(p => new { p.MatchId, p.Status })
                .ToList();
            var invited = context.MatchInvites.AsNoTracking()
                .Where(i => i.InviteeId == playerId && i.Status == InviteStatus.Pending)
                .Select(i => i.MatchId)
                .ToList();

            var kinds = new Dictionary<int, string>();
            foreach (var p in mine)
            {
                kinds[p.MatchId] = p.Status == ParticipantStatus.Going ? "going" : "waitlist";
            }
            foreach (var matchId in invited)
            {
                kinds.TryAdd(matchId, "invited"); // zaten katılıyorsam davet sayılmaz
            }
            var ids = kinds.Keys.ToList();
            if (ids.Count == 0) return new List<CalendarItemDto>();

            var matches = (from m in context.Matchs.AsNoTracking()
                           join s in context.Stadiums.AsNoTracking() on m.StadiumId equals s.StadiumId into ms
                           from s in ms.DefaultIfEmpty()
                           where ids.Contains(m.MatchId) && m.MatchDate >= fromDate && m.MatchDate < toDate
                           select new
                           {
                               m.MatchId, m.MatchName, m.MatchDate, StadiumName = s != null ? s.StadiumName : null,
                               m.IsActive, m.UserCount, m.HomeScore, m.AwayScore
                           }).ToList();

            var matchIds = matches.Select(m => m.MatchId).ToList();
            // Going + misafir
            var goingCounts = RosterCounts(context, matchIds);

            return matches
                .OrderBy(m => m.MatchDate)
                .ThenBy(m => m.MatchId)
                .Select(m => new CalendarItemDto
                {
                    MatchId = m.MatchId,
                    MatchName = m.MatchName,
                    MatchDate = m.MatchDate,
                    StadiumName = m.StadiumName,
                    Kind = kinds[m.MatchId],
                    IsActive = m.IsActive,
                    Capacity = m.UserCount,
                    GoingCount = goingCounts.TryGetValue(m.MatchId, out var c) ? c : 0,
                    HomeScore = m.HomeScore,
                    AwayScore = m.AwayScore,
                })
                .ToList();
        }

        public bool DeleteMatch(int matchId, int? actorPlayerId = null)
        {
            using var context = new MatcBuddyContext();
            using var transaction = context.Database.BeginTransaction();

            var match = LockMatch(context, matchId);
            if (match == null) return false;

            // Kurucu, başkalarının katıldığı maçı son dakika iptal ederse kendi puanı düşer (docs/reliability-api.md)
            if (actorPlayerId.HasValue && match.CreatedByPlayerId == actorPlayerId && match.IsActive != RejectedStatus && !IsPast(match)
                && ReliabilityCore.PenaltyFor(match, DateTime.Now).Type.HasValue
                && context.MatchParticipants.Any(p => p.MatchId == matchId && p.PlayerId != actorPlayerId && p.Status == ParticipantStatus.Going))
            {
                ReliabilityCore.AddEvent(context, actorPlayerId.Value, matchId, ReliabilityEventType.OrganizerLateCancel,
                    -P.Int(ParameterKeys.Reliability.OrganizerLateCancelPenalty));
                context.SaveChanges();
                ReliabilityCore.Recompute(context, actorPlayerId.Value);
            }

            // Notification.MatchId FK değil: maça ait bildirimler (ve oyuncu satırları) elle silinir
            var notificationIds = context.Notification.Where(n => n.MatchId == matchId).Select(n => n.NotificationId);
            context.PlayerNotification.Where(pn => notificationIds.Contains(pn.NotificationId)).ExecuteDelete();
            context.Notification.Where(n => n.MatchId == matchId).ExecuteDelete();

            // Tesis yorumları maça NoAction FK ile bağlı: elle silinir
            context.VenueReviews.Where(r => r.MatchId == matchId).ExecuteDelete();

            // Oynanmamış maç iptal edildiyse kadrodakilere, yedeklere ve tesise haber ver (sahaya boşuna gitmesinler).
            // Maç silindiği için bildirim maça bağlanmaz (MatchId = null).
            if (!IsPast(match))
            {
                var recipients = context.MatchParticipants.AsNoTracking()
                    .Where(p => p.MatchId == matchId && (p.Status == ParticipantStatus.Going || p.Status == ParticipantStatus.Waitlist))
                    .Select(p => p.PlayerId)
                    .ToList();
                if (match.PitchId.HasValue)
                {
                    var ownerId = context.Stadiums.AsNoTracking().Where(s => s.StadiumId == match.StadiumId)
                        .Select(s => s.OwnerPlayerId).FirstOrDefault();
                    if (ownerId.HasValue) recipients.Add(ownerId.Value);
                }
                recipients = recipients.Where(id => id != actorPlayerId).Distinct().ToList();
                if (recipients.Count > 0)
                {
                    NotificationWriter.Add(context, NotificationType.MatchRejection,
                        Msg.GetDefault(MessageKeys.Match.NotifCancelled, match.MatchName ?? string.Empty, match.MatchDate),
                        null, match.StadiumId, recipients);
                    context.SaveChanges();
                }
            }

            // Yorumlar, kadro, beğeni, oy, puan, davet, aktivite ve MatchTeam FK cascade ile silinir
            context.Matchs.Remove(match);
            context.SaveChanges();
            transaction.Commit();
            return true;
        }

        // ------------------------------------------------------------------
        // Hatırlatma
        // ------------------------------------------------------------------

        public int SendDueReminders()
        {
            using var context = new MatcBuddyContext();
            var now = DateTime.Now;
            var until = now.AddHours(P.Int(ParameterKeys.Match.ReminderLeadHours));

            var due = (from p in context.MatchParticipants
                       join m in context.Matchs on p.MatchId equals m.MatchId
                       where p.Status == ParticipantStatus.Going
                             && !p.Confirmed
                             && p.ReminderSentAt == null
                             && m.IsActive != RejectedStatus
                             && m.MatchDate > now
                             && m.MatchDate <= until
                       select p).ToList();
            if (due.Count == 0) return 0;

            var matchIds = due.Select(p => p.MatchId).Distinct().ToList();
            var matches = (from m in context.Matchs.AsNoTracking()
                           join s in context.Stadiums.AsNoTracking() on m.StadiumId equals s.StadiumId
                           where matchIds.Contains(m.MatchId)
                           select new { m.MatchId, m.MatchName, m.MatchDate, s.StadiumName }).ToList();

            foreach (var group in due.GroupBy(p => p.MatchId))
            {
                var match = matches.FirstOrDefault(m => m.MatchId == group.Key);
                if (match == null) continue;

                AddNotification(context, NotificationType.MatchReminder,
                    Msg.GetDefault(MessageKeys.Match.NotifReminder, match.MatchName, match.MatchDate, match.StadiumName),
                    group.Key, group.Select(p => p.PlayerId));

                foreach (var participant in group)
                {
                    participant.ReminderSentAt = now;
                }
            }

            // Tek SaveChanges: bildirimler ve ReminderSentAt birlikte (atomik) yazılır
            context.SaveChanges();
            return due.Count;
        }
    }
}
