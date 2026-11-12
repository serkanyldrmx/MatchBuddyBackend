using MatcBuddy.Core.Errors;
using MatcBuddy.Core.Localization;

namespace MatcBuddy.Core.Utilities.Results
{
    /// <summary>İşlem sonucunun HTTP karşılığını belirlemek için durum kodu.</summary>
    public enum OperationStatus
    {
        Ok,
        BadRequest,
        Forbidden,
        NotFound,
        Conflict,
        Unauthorized
    }

    /// <summary>
    /// Başarı/başarısızlık + neden (400 / 401 / 403 / 404 / 409) + hata kodu taşıyan sonuç.
    /// Hata mesajı, kod üzerinden kaynak dosyasından (Messages.resx, istek dili) çözülür.
    /// Kontrolcü <c>this.ToHttp(result)</c> ile standart hata gövdesine çevirir (docs/platform-api.md).
    /// </summary>
    public class OperationResult<T> : DataResult<T>
    {
        public OperationResult(T data, bool success, string message, OperationStatus status)
            : base(data, success, message)
        {
            Status = status;
        }

        private OperationResult(OperationStatus status, string code, object?[]? args)
            : base(default!, false, Msg.Get(code, args ?? Array.Empty<object?>()))
        {
            Status = status;
            Code = code;
            Args = args ?? Array.Empty<object?>();
        }

        public OperationStatus Status { get; }

        /// <summary>Alan bazlı hatalar (errors sözlüğü): alan adı → mesajlar. Opsiyonel.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public IDictionary<string, string[]>? Errors { get; private set; }

        /// <summary>Başarılı sonuç. Mesaj verilecekse Msg.Get(MessageKeys.X) ile kaynaktan alınmalıdır.</summary>
        public static OperationResult<T> Ok(T data, string message = "") =>
            new(data, true, message, OperationStatus.Ok);

        public static OperationResult<T> Fail(OperationStatus status, string code, params object?[] args) =>
            new(status, code, args);

        public static OperationResult<T> Fail(AppError error) =>
            new(error.Status, error.Code, error.Args);

        public static OperationResult<T> BadRequest(string code, params object?[] args) =>
            new(OperationStatus.BadRequest, code, args);

        public static OperationResult<T> BadRequest(AppError error) =>
            new(error.Status, error.Code, error.Args);

        public static OperationResult<T> Forbidden(string code = ErrorCodes.Auth.Forbidden, params object?[] args) =>
            new(OperationStatus.Forbidden, code, args);

        public static OperationResult<T> NotFound(string code = ErrorCodes.Match.NotFound, params object?[] args) =>
            new(OperationStatus.NotFound, code, args);

        public static OperationResult<T> Conflict(string code, params object?[] args) =>
            new(OperationStatus.Conflict, code, args);

        /// <summary>Alan bazlı hata (errors: { field: [message] }) ile 400.</summary>
        public static OperationResult<T> FieldError(string field, string code, params object?[] args)
        {
            var r = new OperationResult<T>(OperationStatus.BadRequest, code, args);
            r.Errors = new Dictionary<string, string[]> { [field] = new[] { r.Message } };
            return r;
        }

        /// <summary>Başka türdeki başarısız sonucu (kod, durum, argümanlar korunarak) bu türe taşır.</summary>
        public static OperationResult<T> From<TOther>(OperationResult<TOther> failed)
        {
            var r = new OperationResult<T>(failed.Status == OperationStatus.Ok ? OperationStatus.BadRequest : failed.Status,
                failed.Code ?? ErrorCodes.Common.OperationFailed, failed.Args);
            r.Errors = failed.Errors;
            return r;
        }
    }
}
