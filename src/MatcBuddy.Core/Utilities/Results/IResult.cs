namespace MatcBuddy.Core.Utilities.Results
{
    //temel voidler için başlangıç
    public interface IResult
    {
        bool Success { get; }
        string Message { get; }

        /// <summary>Hata kodu (ErrorCodes kataloğundan). Başarılı sonuçlarda null.</summary>
        string? Code { get; }
    }
}
