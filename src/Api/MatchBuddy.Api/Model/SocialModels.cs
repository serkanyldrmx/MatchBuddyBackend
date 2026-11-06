using MatchBuddy.Core.Utilities.Results;
using Microsoft.AspNetCore.Mvc;

namespace MatchBuddy.Api.Model
{
    // ---- /api/Social istek gövdeleri ----
    public class PlayerIdModel
    {
        public int PlayerId { get; set; }
    }

    public class ReportModel
    {
        public int PlayerId { get; set; }
        public string? Reason { get; set; }
        public string? Details { get; set; }
    }

    public class DeleteAccountModel
    {
        public string? Password { get; set; }
    }

    // ---- /api/Invites ----
    public class InviteToMatchModel
    {
        public int MatchId { get; set; }
        public List<int>? PlayerIds { get; set; }
    }

    public class InviteToTeamModel
    {
        public int TeamId { get; set; }
        public List<int>? PlayerIds { get; set; }
    }

    public class RespondInviteModel
    {
        public string? Kind { get; set; } // "match" | "team"
        public int InviteId { get; set; }
        public bool Accept { get; set; }
    }

    // ---- /api/Admin ----
    public class ReportIdModel
    {
        public int ReportId { get; set; }
    }

    public static class OperationResultHttp
    {
        /// <summary>OperationResult -> HTTP (200 / 400 / 403 / 404). Hata gövdesi: { success: false, message }.</summary>
        public static ActionResult ToHttp<T>(this ControllerBase controller, OperationResult<T> result, object? okBody = null)
        {
            var body = new { Success = result.Success, Message = result.Message };
            return result.Status switch
            {
                OperationStatus.Ok => controller.Ok(okBody ?? body),
                OperationStatus.Forbidden => controller.StatusCode(StatusCodes.Status403Forbidden, body),
                OperationStatus.NotFound => controller.NotFound(body),
                _ => controller.BadRequest(body),
            };
        }
    }
}
