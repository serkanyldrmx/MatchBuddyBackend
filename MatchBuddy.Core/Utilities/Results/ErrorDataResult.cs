using MatchBuddy.Core.Errors;
using MatchBuddy.Core.Localization;

namespace MatchBuddy.Core.Utilities.Results
{
    public class ErrorDataResult<T> : DataResult<T>
    {
        /// <summary>Hata kodu (ErrorCodes) + mesaj yer tutucu değerleri.</summary>
        public ErrorDataResult(string code, params object?[] args) : base(default!, false, Msg.Get(code, args))
        {
            Code = code;
            Args = args;
        }
        public ErrorDataResult() : this(ErrorCodes.Common.OperationFailed)
        {
        }
    }
}
