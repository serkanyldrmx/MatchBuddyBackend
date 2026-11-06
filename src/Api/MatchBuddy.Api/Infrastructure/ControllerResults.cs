using MatchBuddy.Core.Utilities.Results;
using Microsoft.AspNetCore.Mvc;

namespace MatchBuddy.Api.Infrastructure
{
    /// <summary>
    /// Kontrolcülerden standart hata gövdesi döndürmek için yardımcılar. Tüm hata yolları bunları kullanır.
    /// </summary>
    public static class ControllerResults
    {
        public static int HttpStatus(OperationStatus status) => status switch
        {
            OperationStatus.Ok => StatusCodes.Status200OK,
            OperationStatus.Forbidden => StatusCodes.Status403Forbidden,
            OperationStatus.NotFound => StatusCodes.Status404NotFound,
            OperationStatus.Conflict => StatusCodes.Status409Conflict,
            OperationStatus.Unauthorized => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status400BadRequest,
        };

        /// <summary>
        /// OperationResult -> HTTP. Başarılı: 200 + okBody (yoksa { success, message }).
        /// Hata: durum (400/401/403/404/409) + standart hata gövdesi.
        /// </summary>
        public static ActionResult ToHttp<T>(this ControllerBase controller, OperationResult<T> result, object? okBody = null)
        {
            if (result.Success)
                return controller.Ok(okBody ?? new { Success = true, result.Message });
            return controller.Error(HttpStatus(result.Status), result.Code ?? ErrorCodes.Common.OperationFailed,
                result.Message, result.Errors);
        }

        /// <summary>Eski tip Result/ErrorResult hatası -> standart gövde (varsayılan 400).</summary>
        public static ActionResult ToHttp(this ControllerBase controller, MatchBuddy.Core.Utilities.Results.IResult result, int failureStatus = StatusCodes.Status400BadRequest)
        {
            if (result.Success) return controller.Ok(result);
            return controller.Error(failureStatus, result.Code ?? ErrorCodes.Common.OperationFailed, result.Message);
        }

        public static ActionResult Error(this ControllerBase controller, int status, string code, string? message = null,
            IDictionary<string, string[]>? errors = null) =>
            new ObjectResult(ApiErrors.Create(controller.HttpContext, code, message, errors)) { StatusCode = status };

        public static ActionResult Error(this ControllerBase controller, AppError error) =>
            controller.Error(HttpStatus(error.Status), error.Code, error.Message);

        public static ActionResult BadRequestError(this ControllerBase controller, string code, params object?[] args) =>
            controller.Error(StatusCodes.Status400BadRequest, code, Msg.Get(code, args));

        public static ActionResult NotFoundError(this ControllerBase controller, string code, params object?[] args) =>
            controller.Error(StatusCodes.Status404NotFound, code, Msg.Get(code, args));

        public static ActionResult ForbiddenError(this ControllerBase controller, string code = ErrorCodes.Auth.Forbidden, params object?[] args) =>
            controller.Error(StatusCodes.Status403Forbidden, code, Msg.Get(code, args));

        public static ActionResult UnauthorizedError(this ControllerBase controller, string code = ErrorCodes.Auth.Unauthorized, params object?[] args) =>
            controller.Error(StatusCodes.Status401Unauthorized, code, Msg.Get(code, args));

        /// <summary>Alan bazlı 400: errors = { field: [message] }.</summary>
        public static ActionResult FieldError(this ControllerBase controller, string field, string code, params object?[] args)
        {
            var message = Msg.Get(code, args);
            return controller.Error(StatusCodes.Status400BadRequest, code, message,
                new Dictionary<string, string[]> { [field] = new[] { message } });
        }
    }
}
