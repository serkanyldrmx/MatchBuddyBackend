using MatcBuddy.Api.Model;
using MatcBuddy.Api.Security;
using MatcBuddy.Business.Abstract;
using MatcBuddy.Core.Utilities.Results;
using MatcBuddy.Entities.DTOs;
using MatcBuddy.Entities.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MatcBuddy.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MatchController : ControllerBase
    {
        private readonly IMatchService _matchService;
        private readonly ITeamService _teamService;
        private readonly IMatchParticipationService _participationService;
        private readonly IVenueService _venueService;

        public MatchController(IMatchService matchService, ITeamService teamService, IMatchParticipationService participationService,
            IVenueService venueService)
        {
            _matchService = matchService;
            _teamService = teamService;
            _participationService = participationService;
            _venueService = venueService;
        }

        // OperationResult -> HTTP cevabı (200 / 400 / 403 / 404); hata gövdesi standart (code, message)
        private IActionResult FromOperation<T>(OperationResult<T> result, object? okBody = null) =>
            this.ToHttp(result, okBody);

        //Maç kurma = anlaşmalı tesiste rezervasyon talebi (docs/venues-api.md); işletme onayı ile aktifleşir.
        //Kurucu = giriş yapan oyuncu; otomatik olarak kadroya Going (Home, onaylı) eklenir.
        //Eski gövde (stadiumId, isActive; pitchId yok) -> 400 "Lütfen bir saha ve saat seçin."
        [HttpPost("SaveMatch")]
        public IActionResult SaveMatch(MatchModel matchModel)
        {
            if (User.IsOwner())
                return this.ForbiddenError(ErrorCodes.Match.OwnerCannotCreate);

            var result = _venueService.CreateReservation(User.GetPlayerId(), new SaveMatchInput
            {
                MatchName = matchModel.MatchName,
                Description = matchModel.Description,
                UserCount = matchModel.UserCount,
                PitchId = matchModel.PitchId,
                MatchDate = matchModel.MatchDate,
                DurationMinutes = matchModel.DurationMinutes,
                CleatCount = matchModel.CleatCount,
                CameraRequested = matchModel.CameraRequested,
                HomeTeamId = matchModel.HomeTeamId,
                AwayTeamId = matchModel.AwayTeamId,
                LookingForOpponent = matchModel.LookingForOpponent,
                MinReliability = matchModel.MinReliability,
                RepeatWeekly = matchModel.RepeatWeekly,
            });
            return this.ToHttp(result, result.Success
                ? new { Success = true, result.Message, MatchId = result.Data, Status = MatchStatus.Pending }
                : null);
        }

        /// <summary>
        /// Harici (anlaşmasız) sahada maç kur: oyuncu sahayı telefonla kendisi ayarladı (docs/external-venues-api.md).
        /// İşletme onayı yoktur, maç doğrudan onaylı açılır. Saha: önceki harici sahalarımdan biri (externalVenueId)
        /// ya da yeni (venueName, address, city, district, phone, location).
        /// </summary>
        [HttpPost("SaveExternalMatch")]
        public IActionResult SaveExternalMatch(ExternalMatchInput input)
        {
            if (User.IsOwner())
                return this.ForbiddenError(ErrorCodes.Match.OwnerCannotCreate);

            var result = _venueService.CreateExternalMatch(User.GetPlayerId(), input);
            return this.ToHttp(result, result.Success
                ? new { Success = true, result.Message, MatchId = result.Data, Status = MatchStatus.Approved }
                : null);
        }

        /// <summary>Harici sahadaki maçın saat / süre / ücret / saha bilgisini değiştir (kurucu ya da admin). Kadroya bildirim gider.</summary>
        [HttpPost("UpdateExternalMatch")]
        public IActionResult UpdateExternalMatch(UpdateExternalMatchInput input) =>
            this.ToHttp(_venueService.UpdateExternalMatch(User.GetPlayerId(), User.IsAdmin(), input));

        /// <summary>Daha önce eklediğim harici sahalar (son kullanılan önce).</summary>
        [HttpGet("MyExternalVenues")]
        public ActionResult<List<ExternalVenueDto>> MyExternalVenues() => _venueService.MyExternalVenues(User.GetPlayerId());

        /// <summary>Paylaşılabilir davet linki (WhatsApp vb.): { code, url, text }. Link ilk istekte oluşur, sonra aynı kalır.</summary>
        [HttpPost("ShareLink")]
        public IActionResult ShareLink(MatchIdModel model, [FromServices] MatcBuddy.Api.Services.ShareService share)
        {
            var result = share.GetOrCreate(model.MatchId);
            return result.Success ? Ok(result.Data) : this.ToHttp(result);
        }

        /// <summary>Maçı (bilgileri + davet linki) üyesi olduğun gruplara mesaj olarak gönderir. Yanıt: { groupIds, message }.</summary>
        [HttpPost("ShareToGroups")]
        public IActionResult ShareToGroups(ShareToGroupsModel model, [FromServices] MatcBuddy.Api.Services.ShareService share,
            [FromServices] MatcBuddy.Api.Hubs.RealtimeNotifier realtime)
        {
            var me = User.GetPlayerId();
            var result = share.ShareToGroups(me, User.IsAdmin(), model.MatchId, model.GroupIds, model.Note);
            if (!result.Success || result.Data == null) return this.ToHttp(result);
            foreach (var groupId in result.Data) _ = realtime.GroupMessageAsync(groupId, me);
            return Ok(new { groupIds = result.Data, message = result.Message });
        }

        /// <summary>Davet linkiyle katıl (kayıt / girişten sonra). Yanıt: { matchId, status, message }.</summary>
        [HttpPost("JoinByCode")]
        public IActionResult JoinByCode(JoinByCodeModel model, [FromServices] MatcBuddy.Api.Services.ShareService share)
        {
            var matchId = share.MatchIdOf(model.Code);
            if (matchId == null) return this.NotFoundError(ErrorCodes.Share.NotFound);
            if (User.IsOwner()) return this.ForbiddenError(ErrorCodes.Match.OwnerCannotJoin);
            var result = _participationService.Join(matchId.Value, User.GetPlayerId());
            if (!result.Success) return FromOperation(result);
            return Ok(new { Success = true, MatchId = matchId.Value, Status = result.Data, result.Message });
        }

        // Rakip takım olarak katılma: lookingForOpponent maçlarda, takımın üyesi
        [HttpPost("ClaimOpponent")]
        public IActionResult ClaimOpponent(ClaimOpponentInput model)
        {
            return this.ToHttp(_venueService.ClaimOpponent(User.GetPlayerId(), model));
        }

        // Maç bilgilerini düzenle (yetki: canManage): ad, açıklama, oyuncu sayısı. Admin: tarih / durum da.
        // Sözleşme: docs/match-participation-api.md "UpdateMatch"
        [HttpPost("UpdateMatch")]
        public IActionResult UpdateMatch(MatchModel matchModel) =>
            this.ToHttp(_participationService.UpdateInfo(User.GetPlayerId(), User.IsAdmin(), new UpdateMatchInfoInput
            {
                MatchId = matchModel.MatchId,
                MatchName = matchModel.MatchName,
                Description = matchModel.Description,
                UserCount = matchModel.UserCount,
                MatchDate = matchModel.MatchDate,
                IsActive = matchModel.IsActive == 0 ? null : matchModel.IsActive,
            }));

        [HttpPost("StopRepeat")]
        public IActionResult StopRepeat(MatchModel model) =>
            this.ToHttp(MatcBuddy.DataAccess.Concrete.EntityFramework.MatchSeriesService.Stop(model.MatchId, User.GetPlayerId(), User.IsAdmin()));

        [HttpPost("DeleteMatch")]
        public IActionResult DeleteMatch(MatchModel matchModel)
        {
            var permission = _participationService.CheckCanManage(matchModel.MatchId, User.GetPlayerId(), User.IsAdmin());
            if (!permission.Success) return FromOperation(permission);

            // Yorumlar, kadro, davetler, aktiviteler (FK cascade) ve maça ait bildirimler de silinir
            var result = _participationService.DeleteMatch(matchModel.MatchId, User.GetPlayerId());
            if (result.Success)
            {
                return Ok(result);
            }
            return this.ToHttp(result, StatusCodes.Status404NotFound);
        }

        //Tek bir maçı getirmek için
        [HttpGet("GetMatchById")]
        public Match GetMatchById([FromQuery] int matchId)
        {
            return _matchService.GetById(matchId).Data;
        }

        [HttpGet("GetMatchList")]
        public List<Match> GetMatchList()
        {
            return _matchService.GetAll().Data;
        }

        [HttpGet("GetMatchComments")]
        public List<MatchComentsDto> GetMatchComments([FromQuery] int matchId)
        {
            return _matchService.GetMatchComents(matchId).Data;
        }

        // Eski uç nokta (geri uyumluluk): ToggleLike gibi çalışır ama sadece beğenir
        [HttpGet("MatchLiked")]
        public IActionResult MatchLiked([FromQuery] int matchId)
        {
            var result = _participationService.Like(matchId, User.GetPlayerId());
            if (!result.Success) return FromOperation(result);
            return Ok(new Result(true, result.Message));
        }

        //bir maçtaki takımları ve bu takımlardaki oyuncu bilgilerini getirir
        [HttpGet("GetMatchTeamInfo")]
        public List<MatchTeamDto> GetMatchTeamInfo([FromQuery] int matchId)
        {
            return _matchService.GetMatchTeam(matchId).Data;
        }

        //Maça takım ekler. Takımları henüz belirlenmemiş (2'den az takımlı) maça giriş yapmış herkes takım atayabilir;
        //maçta zaten 2 takım varsa sadece admin değişiklik yapabilir.
        [HttpPost("SaveMatchTeam")]
        public IActionResult SaveMatchTeam(MatchTeamModel matchTeamModel)
        {
            var existingTeamIds = _matchService.GetMatchTeam(matchTeamModel.MatchId).Data
                .Select(t => t.TeamId).Distinct().ToList();
            if (existingTeamIds.Contains(matchTeamModel.TeamId))
            {
                return this.BadRequestError(ErrorCodes.Match.TeamAlreadyAdded);
            }
            if (!User.IsAdmin() && existingTeamIds.Count >= 2)
            {
                return Forbid();
            }

            var matchTeam = new MatchTeam()
            {
                MatchId = matchTeamModel.MatchId,
                TeamId = matchTeamModel.TeamId
            };
            var result = _matchService.AddMatchTeam(matchTeam);
            if (result.Success)
            {
                return Ok(result);
            }
            return this.ToHttp(result);
        }

        //Maçın durumunu günceller (onay/red): sadece admin
        [Authorize(Roles = Roles.Admin)]
        [HttpPost("MatchStatusUpdate")]
        public IActionResult MatchStatusUpdate(MatchStatusModel matchStatusModel)
        {
            // Admin geçersiz kılma; asıl yol işletmenin RespondReservation uç noktası
            return this.ToHttp(_venueService.AdminSetStatus(matchStatusModel.MatchId, matchStatusModel.Status, matchStatusModel.Reason));
        }

        // ------------------------------------------------------------------
        // Maç katılımı, sonuç, MVP ve puanlama (docs/match-participation-api.md)
        // ------------------------------------------------------------------

        // Ana sayfa listesi: tüm maçlar (frontend filtreler)
        [HttpGet("GetMatchFeed")]
        public ActionResult<List<MatchFeedItemDto>> GetMatchFeed()
        {
            return _participationService.GetMatchFeed(User.GetPlayerId()).Data;
        }

        [HttpGet("GetMatchDetail")]
        public ActionResult<MatchDetailDto> GetMatchDetail([FromQuery] int matchId)
        {
            var result = _participationService.GetMatchDetail(matchId, User.GetPlayerId(), User.IsAdmin());
            if (!result.Success || result.Data == null)
            {
                return this.NotFoundError(result.Code ?? ErrorCodes.Match.NotFound);
            }
            return result.Data;
        }

        [HttpPost("Join")]
        public IActionResult Join(JoinMatchModel model)
        {
            if (User.IsOwner())
                return this.ForbiddenError(ErrorCodes.Match.OwnerCannotJoin);
            var result = _participationService.Join(model.MatchId, User.GetPlayerId(), model.Side, model.Slot);
            if (!result.Success) return FromOperation(result);
            return Ok(new JoinResponse { Success = true, Message = result.Message, Status = result.Data });
        }

        [HttpPost("Leave")]
        public IActionResult Leave(LeaveMatchModel model)
        {
            // Mazeret (reasonCode) + not isteğe bağlı; dönüş { success, message, penalty, newScore } — docs/reliability-api.md
            var result = _participationService.Leave(model.MatchId, User.GetPlayerId(), model.ReasonCode, model.Note);
            if (!result.Success || result.Data == null) return this.ToHttp(result);
            return Ok(result.Data);
        }

        [HttpPost("Confirm")]
        public IActionResult Confirm(MatchIdModel model)
        {
            return FromOperation(_participationService.Confirm(model.MatchId, User.GetPlayerId()));
        }

        [HttpPost("SetSide")]
        public IActionResult SetSide(SetSideModel model)
        {
            return FromOperation(_participationService.SetSide(
                model.MatchId, User.GetPlayerId(), User.IsAdmin(), model.PlayerId, model.Side));
        }

        /// <summary>Sahada oyuncuların yerini/tarafını kaydeder (sürükle-bırak). Yetki: canManage.</summary>
        [HttpPost("ArrangeLineup")]
        public IActionResult ArrangeLineup(ArrangeLineupModel model)
        {
            return this.ToHttp(_participationService.ArrangeLineup(model.MatchId, User.GetPlayerId(), User.IsAdmin(), model.Items));
        }

        // ---- Misafir oyuncular (docs/match-participation-api.md "Ek: Misafir oyuncular"); yetki: canManage ----

        [HttpPost("AddGuest")]
        public IActionResult AddGuest(AddGuestModel model)
        {
            var result = _participationService.AddGuest(model.MatchId, User.GetPlayerId(), User.IsAdmin(), model.Side, model.Name, model.Note);
            return this.ToHttp(result, result.Success
                ? new { Success = true, result.Message, GuestId = result.Data }
                : null);
        }

        [HttpPost("UpdateGuest")]
        public IActionResult UpdateGuest(UpdateGuestModel model)
        {
            return this.ToHttp(_participationService.UpdateGuest(model.GuestId, User.GetPlayerId(), User.IsAdmin(), model.Name, model.Note, model.Side));
        }

        [HttpPost("RemoveGuest")]
        public IActionResult RemoveGuest(GuestIdModel model)
        {
            return this.ToHttp(_participationService.RemoveGuest(model.GuestId, User.GetPlayerId(), User.IsAdmin()));
        }

        [HttpPost("SubmitResult")]
        public IActionResult SubmitResult(SubmitResultModel model)
        {
            return FromOperation(_participationService.SubmitResult(
                model.MatchId, User.GetPlayerId(), User.IsAdmin(), model.HomeScore, model.AwayScore, model.Stats));
        }

        [HttpPost("VoteMvp")]
        public IActionResult VoteMvp(VoteMvpModel model)
        {
            return FromOperation(_participationService.VoteMvp(model.MatchId, User.GetPlayerId(), model.VotedPlayerId));
        }

        [HttpPost("RatePlayers")]
        public IActionResult RatePlayers(RatePlayersModel model)
        {
            return FromOperation(_participationService.RatePlayers(model.MatchId, User.GetPlayerId(), model.Ratings));
        }

        // Takvim: Going / yedek / bekleyen davetli olduğum maçlar. from/to boşsa bu ayın başından 2 ay sonrasına kadar
        [HttpGet("MyCalendar")]
        public ActionResult<List<CalendarItemDto>> MyCalendar([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var result = _participationService.GetMyCalendar(User.GetPlayerId(), from, to);
            if (!result.Success)
            {
                return this.ToHttp(result);
            }
            return result.Data;
        }

        [HttpPost("ToggleLike")]
        public IActionResult ToggleLike(MatchIdModel model)
        {
            var result = _participationService.ToggleLike(model.MatchId, User.GetPlayerId());
            if (!result.Success) return FromOperation(result);
            return Ok(result.Data);
        }
    }
}
