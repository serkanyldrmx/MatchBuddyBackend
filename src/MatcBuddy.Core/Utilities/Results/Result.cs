using System.Text.Json.Serialization;
using MatcBuddy.Core.Localization;

namespace MatcBuddy.Core.Utilities.Results
{
    public class Result : IResult
    {
        public Result(bool success, string mesage): this(success)
        {
            Message = mesage;
        }
        public Result(bool success)
        {
            Success = success;
            Message = null!;
        }

        /// <summary>Hata sonucu: mesaj kaynak dosyasından (Messages.resx) çözülür.</summary>
        protected Result(string code, object?[]? args) : this(false)
        {
            Code = code;
            Args = args ?? Array.Empty<object?>();
            Message = Msg.Get(code, Args);
        }

        public bool Success { get; }
        public string Message { get; protected set; }

        /// <summary>Hata kodu (ErrorCodes). Başarılı sonuçlarda null ve JSON'a yazılmaz.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Code { get; protected set; }

        /// <summary>Mesaj yer tutucularının değerleri (ileride başka dilde yeniden çözmek için).</summary>
        [JsonIgnore]
        public object?[] Args { get; protected set; } = Array.Empty<object?>();
    }
}
