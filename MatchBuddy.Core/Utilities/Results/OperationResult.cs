namespace MatchBuddy.Core.Utilities.Results
{
    /// <summary>İşlem sonucunun HTTP karşılığını belirlemek için durum kodu.</summary>
    public enum OperationStatus
    {
        Ok,
        BadRequest,
        Forbidden,
        NotFound
    }

    /// <summary>
    /// Başarı/başarısızlık + neden (400 / 403 / 404) taşıyan sonuç.
    /// Kontrolcü Status'a göre uygun HTTP cevabını döner.
    /// </summary>
    public class OperationResult<T> : DataResult<T>
    {
        public OperationResult(T data, bool success, string message, OperationStatus status)
            : base(data, success, message)
        {
            Status = status;
        }

        public OperationStatus Status { get; }

        public static OperationResult<T> Ok(T data, string message = "") =>
            new(data, true, message, OperationStatus.Ok);

        public static OperationResult<T> BadRequest(string message) =>
            new(default!, false, message, OperationStatus.BadRequest);

        public static OperationResult<T> Forbidden(string message = "Bu işlem için yetkiniz yok.") =>
            new(default!, false, message, OperationStatus.Forbidden);

        public static OperationResult<T> NotFound(string message = "Maç bulunamadı.") =>
            new(default!, false, message, OperationStatus.NotFound);
    }
}
