using MatcBuddy.Core.Errors;

namespace MatcBuddy.Core.Utilities.Results
{
    public class ErrorResult : Result
    {
        /// <summary>Hata kodu (ErrorCodes) + mesaj yer tutucu değerleri.</summary>
        public ErrorResult(string code, params object?[] args) : base(code, args)
        {
        }
        public ErrorResult() : base(ErrorCodes.Common.OperationFailed, null)
        {
        }
    }
}
