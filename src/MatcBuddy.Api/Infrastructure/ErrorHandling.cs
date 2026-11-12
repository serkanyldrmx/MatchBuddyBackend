using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace MatcBuddy.Api.Infrastructure
{
    /// <summary>
    /// Kontrolcülerden dönen ve standart gövdede olmayan her hata yanıtını (eski tip gövdeler, ProblemDetails,
    /// gövdesiz NotFound()/BadRequest() vb.) standart hata gövdesine çeviren güvenlik ağı.
    /// </summary>
    public sealed class StandardErrorResultFilter : IAlwaysRunResultFilter
    {
        public void OnResultExecuting(ResultExecutingContext context)
        {
            var http = context.HttpContext;
            switch (context.Result)
            {
                case ObjectResult { Value: ApiErrorResponse body } obj:
                    body.TraceId ??= ApiErrors.TraceId(http);
                    break;
                case ObjectResult obj when (obj.StatusCode ?? 200) >= 400:
                    context.Result = Convert(http, obj.StatusCode!.Value, obj.Value);
                    break;
                case StatusCodeResult sc when sc.StatusCode >= 400:
                    context.Result = Convert(http, sc.StatusCode, null);
                    break;
            }
        }

        public void OnResultExecuted(ResultExecutedContext context) { }

        private static ObjectResult Convert(HttpContext http, int status, object? value)
        {
            string code = ApiErrors.CodeForStatus(status);
            string? message = null;
            IDictionary<string, string[]>? errors = null;
            switch (value)
            {
                case MatcBuddy.Core.Utilities.Results.IResult r:
                    code = r.Code ?? code;
                    message = r.Message;
                    break;
                case string s when !string.IsNullOrWhiteSpace(s):
                    message = s;
                    break;
                case ValidationProblemDetails vpd:
                    code = ErrorCodes.Validation.Failed;
                    errors = vpd.Errors.ToDictionary(e => ModelValidation.NormalizeKey(e.Key), e => e.Value);
                    break;
                case ProblemDetails:
                case null:
                    break;
                default:
                    // Anonim nesne: { Message, Code } özellikleri varsa kullanılır
                    var type = value.GetType();
                    message = type.GetProperty("Message", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)?.GetValue(value) as string;
                    code = type.GetProperty("Code", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)?.GetValue(value) as string ?? code;
                    break;
            }
            return new ObjectResult(ApiErrors.Create(http, code, message, errors)) { StatusCode = status };
        }
    }

    /// <summary>Otomatik model doğrulaması (ApiController 400) → VALIDATION_FAILED + errors.</summary>
    public static class ModelValidation
    {
        public static string NormalizeKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return "body";
            key = key.StartsWith("$.") ? key[2..] : key.TrimStart('$');
            if (key.Length == 0) return "body";
            return string.Join('.', key.Split('.').Select(seg => seg.Length > 0 ? char.ToLowerInvariant(seg[0]) + seg[1..] : seg));
        }

        public static IActionResult InvalidModelStateResponse(ActionContext context)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var (key, entry) in context.ModelState)
            {
                if (entry.Errors.Count == 0) continue;
                var name = NormalizeKey(key);
                var messages = entry.Errors
                    .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) || e.Exception != null
                        ? Msg.Get(ErrorCodes.Validation.InvalidFormat)
                        : e.ErrorMessage)
                    .Distinct().ToArray();
                errors[name] = errors.TryGetValue(name, out var existing) ? existing.Concat(messages).Distinct().ToArray() : messages;
            }
            // Gövde hiç okunamadıysa (boş/bozuk JSON) tek "body" hatası yeterli
            var body = ApiErrors.Create(context.HttpContext, ErrorCodes.Validation.Failed, null, errors);
            return new BadRequestObjectResult(body);
        }

        /// <summary>Model bağlama varsayılan (İngilizce) mesajlarını kaynak dosyasına bağlar.</summary>
        public static void LocalizeModelBindingMessages(DefaultModelBindingMessageProvider p)
        {
            p.SetValueMustNotBeNullAccessor(v => Msg.Get(ErrorCodes.Validation.ValueInvalid, v));
            p.SetMissingBindRequiredValueAccessor(name => Msg.Get(ErrorCodes.Validation.Required, name));
            p.SetMissingKeyOrValueAccessor(() => Msg.Get(ErrorCodes.Validation.ValueMissing));
            p.SetMissingRequestBodyRequiredValueAccessor(() => Msg.Get(ErrorCodes.Validation.BodyRequired));
            p.SetAttemptedValueIsInvalidAccessor((v, name) => Msg.Get(ErrorCodes.Validation.InvalidValue, v, name));
            p.SetNonPropertyAttemptedValueIsInvalidAccessor(v => Msg.Get(ErrorCodes.Validation.ValueInvalid, v));
            p.SetUnknownValueIsInvalidAccessor(name => Msg.Get(ErrorCodes.Validation.InvalidFormat));
            p.SetNonPropertyUnknownValueIsInvalidAccessor(() => Msg.Get(ErrorCodes.Validation.UnknownValueInvalid));
            p.SetValueIsInvalidAccessor(v => Msg.Get(ErrorCodes.Validation.ValueInvalid, v));
            p.SetValueMustBeANumberAccessor(name => Msg.Get(ErrorCodes.Validation.MustBeNumber, name));
            p.SetNonPropertyValueMustBeANumberAccessor(() => Msg.Get(ErrorCodes.Validation.MustBeNumber, Msg.Get("LABEL_COMMON_VALUE")));
        }
    }

    /// <summary>
    /// DataAnnotations özniteliklerinin (örtük [Required] dahil) mesajlarını kaynak dosyasına yönlendirir.
    /// Özellikler her çağrıda Msg.Get ile istek dilinde çözülür.
    /// </summary>
    public sealed class LocalizedValidationMetadataProvider : IValidationMetadataProvider
    {
        public void CreateValidationMetadata(ValidationMetadataProviderContext context)
        {
            foreach (var attribute in context.ValidationMetadata.ValidatorMetadata.OfType<ValidationAttribute>())
            {
                if (!string.IsNullOrEmpty(attribute.ErrorMessage) || attribute.ErrorMessageResourceType != null) continue;
                var name = attribute switch
                {
                    RequiredAttribute => nameof(ValidationMessages.Required),
                    StringLengthAttribute => nameof(ValidationMessages.TooLong),
                    MaxLengthAttribute => nameof(ValidationMessages.TooLong),
                    MinLengthAttribute => nameof(ValidationMessages.TooShort),
                    RangeAttribute => nameof(ValidationMessages.OutOfRange),
                    EmailAddressAttribute => nameof(ValidationMessages.InvalidEmail),
                    _ => nameof(ValidationMessages.InvalidFormat),
                };
                attribute.ErrorMessageResourceType = typeof(ValidationMessages);
                attribute.ErrorMessageResourceName = name;
            }
        }
    }

    /// <summary>ValidationAttribute.ErrorMessageResourceType hedefi (statik özellikler, istek dilinde).</summary>
    public static class ValidationMessages
    {
        public static string Required => Msg.Get(ErrorCodes.Validation.Required);
        public static string TooLong => Msg.Get(ErrorCodes.Validation.TooLong);
        public static string TooShort => Msg.Get(ErrorCodes.Validation.TooShort);
        public static string OutOfRange => Msg.Get(ErrorCodes.Validation.OutOfRange);
        public static string InvalidEmail => Msg.Get(ErrorCodes.Validation.InvalidEmail);
        public static string InvalidFormat => Msg.Get(ErrorCodes.Validation.InvalidFormat);
    }

    /// <summary>İşlenmeyen hata → 500 COMMON_INTERNAL_ERROR (ayrıntı yalnızca Development'ta).</summary>
    public sealed class StandardExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<StandardExceptionHandler> _logger;
        private readonly IHostEnvironment _env;

        public StandardExceptionHandler(ILogger<StandardExceptionHandler> logger, IHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
        {
            if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
            {
                context.Response.StatusCode = 499; // istemci bağlantıyı kapattı
                return true;
            }
            if (exception is BadHttpRequestException bad)
            {
                await ApiErrors.WriteAsync(context, bad.StatusCode, ApiErrors.CodeForStatus(bad.StatusCode));
                return true;
            }
            _logger.LogError(exception, "İşlenmeyen hata: {Method} {Path}", context.Request.Method, context.Request.Path);
            await ApiErrors.WriteAsync(context, StatusCodes.Status500InternalServerError, ErrorCodes.Common.InternalError,
                detail: _env.IsDevelopment() ? exception.ToString() : null);
            return true;
        }
    }

    /// <summary>Gövdesiz 4xx/5xx yanıtları (rota bulunamadı, 405 vb.) standart gövdeyle doldurur.</summary>
    public static class StatusCodeBodies
    {
        public static Task WriteAsync(StatusCodeContext context)
        {
            var http = context.HttpContext;
            var status = http.Response.StatusCode;
            if (status < 400 || http.Response.HasStarted) return Task.CompletedTask;
            return ApiErrors.WriteAsync(http, status, ApiErrors.CodeForStatus(status));
        }
    }

    public static class JsonDefaults
    {
        public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    }
}
