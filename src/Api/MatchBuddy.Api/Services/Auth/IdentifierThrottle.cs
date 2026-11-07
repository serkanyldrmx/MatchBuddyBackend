using Microsoft.Extensions.Caching.Memory;

namespace MatchBuddy.Api.Services.Auth
{
    /// <summary>
    /// Şifre sıfırlama için tanımlayıcı (kullanıcı adı / e-posta / telefon) başına saatlik sınır
    /// (auth.passwordResetPerIdentifierPerHour). IP başına sınır "password-reset" rate limit politikasındadır.
    /// Hesabın var olup olmamasından bağımsız sayılır: 429 yanıtı hesap listelemeye yaramaz.
    /// </summary>
    public sealed class IdentifierThrottle
    {
        private sealed class Window
        {
            public int Count;
            public DateTimeOffset ResetsAt;
        }

        private readonly IMemoryCache _cache;
        public IdentifierThrottle(IMemoryCache cache) => _cache = cache;

        /// <summary>İzin verildiyse null; aşıldıysa kalan saniye.</summary>
        public int? Hit(string scope, string? identifier)
        {
            var id = (identifier ?? string.Empty).Trim().ToLowerInvariant();
            if (id.Length == 0) return null;
            var limit = P.Int(ParameterKeys.Auth.PasswordResetPerIdentifierPerHour);
            var key = $"idthrottle:{scope}:{limit}:{id}";
            var now = DateTimeOffset.UtcNow;
            var window = _cache.GetOrCreate(key, e =>
            {
                e.AbsoluteExpiration = now.AddHours(1);
                return new Window { ResetsAt = now.AddHours(1) };
            })!;
            lock (window)
            {
                if (window.Count >= limit) return Math.Max(1, (int)Math.Ceiling((window.ResetsAt - now).TotalSeconds));
                window.Count++;
                return null;
            }
        }
    }
}
