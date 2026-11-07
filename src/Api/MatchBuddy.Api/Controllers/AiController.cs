using MatchBuddy.Api.Security;
using MatchBuddy.Api.Services.Ai;
using MatchBuddy.Core.Utilities.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatchBuddy.Api.Controllers
{
    public class BalanceTeamsModel
    {
        /// <summary>Verilirse maçta "katılıyor" olan oyuncular dengelenir.</summary>
        public int? MatchId { get; set; }
        /// <summary>Maç verilmediyse dengelenecek oyuncular; boşsa ben + takip ettiklerim.</summary>
        public List<int>? PlayerIds { get; set; }
        /// <summary>true + MatchId: sonuç maçın taraflarına (ev/deplasman) yazılır. Yalnızca maçı kuran.</summary>
        public bool Apply { get; set; }
    }

    /// <summary>Maç önerisi ve takım dengeleme (eski Python AI servislerinin yerine) — docs/ai-api.md.</summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AiController : ControllerBase
    {
        private const int MinPlayers = 2;
        private const int MaxPlayers = 40;
        private readonly RecommendationService _service;

        public AiController(RecommendationService service) => _service = service;

        /// <summary>Giriş yapan oyuncu için önerilen yaklaşan maçlar (puana göre sıralı) ve gerekçeleri.</summary>
        [HttpGet("MatchRecommendations")]
        public async Task<IActionResult> MatchRecommendations(CancellationToken ct) =>
            Ok(await _service.RecommendMatchesAsync(User.GetPlayerId(), ct));

        [HttpPost("BalanceTeams")]
        public async Task<IActionResult> BalanceTeams(BalanceTeamsModel model, CancellationToken ct)
        {
            var me = User.GetPlayerId();
            List<int> ids;
            if (model.MatchId is int matchId)
            {
                var match = await _service.FindMatchAsync(matchId, ct);
                if (match == null) return this.NotFoundError(ErrorCodes.Match.NotFound);
                var isOrganizer = match.CreatedByPlayerId == me || User.IsAdmin();
                if (model.Apply && !isOrganizer) return this.ForbiddenError(ErrorCodes.Match.OrganizerOnly);
                if (!isOrganizer && !await _service.IsParticipantAsync(matchId, me, ct))
                    return this.ForbiddenError(ErrorCodes.Match.NotParticipant);
                ids = await _service.GoingPlayerIdsAsync(matchId, ct);
            }
            else
            {
                ids = model.PlayerIds is { Count: > 0 } ? model.PlayerIds : await _service.FriendPoolAsync(me, ct);
            }

            ids = ids.Distinct().ToList();
            if (ids.Count < MinPlayers || ids.Count > MaxPlayers)
                return this.BadRequestError(ErrorCodes.Match.BalancePlayerCount, MinPlayers, MaxPlayers);

            var teams = await _service.BalanceAsync(ids, ct);
            if (model.Apply && model.MatchId is int applyId) await _service.ApplySidesAsync(applyId, teams, ct);
            return Ok(teams);
        }
    }
}
