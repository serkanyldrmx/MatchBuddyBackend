using MatcBuddy.Core.Utilities.Results;

namespace MatcBuddy.Core.Errors
{
    /// <summary>
    /// Kod + yer tutucu değerleri + HTTP karşılığı. Doğrulama yardımcıları string mesaj yerine bunu döner;
    /// <c>OperationResult&lt;T&gt;.Fail(error)</c> ile sonuca çevrilir.
    /// </summary>
    public sealed class AppError
    {
        public AppError(string code, params object?[] args) : this(OperationStatus.BadRequest, code, args) { }

        public AppError(OperationStatus status, string code, params object?[] args)
        {
            Status = status;
            Code = code;
            Args = args ?? Array.Empty<object?>();
        }

        public OperationStatus Status { get; }
        public string Code { get; }
        public object?[] Args { get; }

        /// <summary>İstek dilinde çözülmüş mesaj.</summary>
        public string Message => Localization.Msg.Get(Code, Args);

        public static AppError Bad(string code, params object?[] args) => new(OperationStatus.BadRequest, code, args);
        public static AppError NotFound(string code, params object?[] args) => new(OperationStatus.NotFound, code, args);
        public static AppError Forbidden(string code, params object?[] args) => new(OperationStatus.Forbidden, code, args);
    }
}
