using System.Diagnostics;
using System.Text.Json.Serialization;

namespace MatchBuddy.Api.Infrastructure
{
    /// <summary>
    /// Standart hata gövdesi (docs/platform-api.md):
    /// { "success": false, "code": "MATCH_FULL", "message": "...", "errors": { "alan": ["..."] }, "traceId": "..." }
    /// </summary>
    public sealed class ApiErrorResponse
    {
        public bool Success => false;
        public string Code { get; init; } = ErrorCodes.Common.BadRequest;
        public string Message { get; init; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IDictionary<string, string[]>? Errors { get; init; }

        public string? TraceId { get; set; }

        /// <summary>Yalnızca Development ortamında: işlenmeyen hatanın ayrıntısı.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Detail { get; init; }
    }

    public static class ApiErrors
    {
        public const string TraceHeader = "X-Trace-Id";

        public static string TraceId(HttpContext? context) =>
            Activity.Current?.TraceId.ToString() ?? context?.TraceIdentifier ?? string.Empty;

        public static ApiErrorResponse Create(HttpContext? context, string code, string? message = null,
            IDictionary<string, string[]>? errors = null, params object?[] args) => new()
        {
            Code = code,
            Message = message ?? Msg.Get(code, args),
            Errors = errors,
            TraceId = TraceId(context),
        };

        /// <summary>HTTP durumuna göre varsayılan kod (gövdesiz 4xx/5xx yanıtlar için).</summary>
        public static string CodeForStatus(int status) => status switch
        {
            StatusCodes.Status400BadRequest => ErrorCodes.Common.BadRequest,
            StatusCodes.Status401Unauthorized => ErrorCodes.Auth.Unauthorized,
            StatusCodes.Status403Forbidden => ErrorCodes.Auth.Forbidden,
            StatusCodes.Status404NotFound => ErrorCodes.Common.NotFound,
            StatusCodes.Status405MethodNotAllowed => ErrorCodes.Common.MethodNotAllowed,
            StatusCodes.Status409Conflict => ErrorCodes.Common.Conflict,
            StatusCodes.Status413PayloadTooLarge => ErrorCodes.Common.PayloadTooLarge,
            StatusCodes.Status415UnsupportedMediaType => ErrorCodes.Common.UnsupportedMediaType,
            StatusCodes.Status429TooManyRequests => ErrorCodes.Common.RateLimited,
            StatusCodes.Status502BadGateway or StatusCodes.Status504GatewayTimeout => ErrorCodes.Common.UpstreamError,
            StatusCodes.Status503ServiceUnavailable => ErrorCodes.Common.ServiceUnavailable,
            >= 500 => ErrorCodes.Common.InternalError,
            _ => ErrorCodes.Common.BadRequest,
        };

        public static Task WriteAsync(HttpContext context, int status, string code, string? message = null,
            IDictionary<string, string[]>? errors = null, string? detail = null)
        {
            context.Response.StatusCode = status;
            var body = Create(context, code, message, errors);
            if (detail != null) body = new ApiErrorResponse
            {
                Code = body.Code, Message = body.Message, Errors = body.Errors, TraceId = body.TraceId, Detail = detail
            };
            return context.Response.WriteAsJsonAsync(body, context.RequestAborted);
        }
    }
}
