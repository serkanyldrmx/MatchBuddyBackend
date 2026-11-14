using System.Security.Cryptography;
using System.Text;
using MatcBuddy.Business.Abstract;
using MatcBuddy.Business.Email;

namespace MatcBuddy.Api.Services
{
    /// <summary>E-postalardaki bağlantılar: site adresi ve imzalı (HMAC) abonelikten çıkma linki.</summary>
    public sealed class EmailLinks : IEmailLinks
    {
        private readonly byte[] _key;

        public EmailLinks(IConfiguration configuration)
        {
            FrontendBaseUrl = (configuration["Auth:FrontendBaseUrl"] ?? "http://localhost:3000").TrimEnd('/');
            _key = Encoding.UTF8.GetBytes("unsubscribe:" + (configuration["Jwt:Key"] ?? string.Empty));
        }

        public string FrontendBaseUrl { get; }

        public string Token(int playerId)
        {
            using var h = new HMACSHA256(_key);
            var hash = h.ComputeHash(Encoding.UTF8.GetBytes($"unsub:{playerId}"));
            return Convert.ToBase64String(hash, 0, 18).Replace('+', '-').Replace('/', '_');
        }

        public bool Verify(int playerId, string? token) =>
            token != null && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Token(playerId)), Encoding.ASCII.GetBytes(token));

        // Site üzerinden (Vercel /api yönlendirmesi) API'ye gider
        public string UnsubscribeUrl(int playerId) => $"{FrontendBaseUrl}/api/Email/Unsubscribe?p={playerId}&t={Token(playerId)}";
    }

    /// <summary>
    /// Dakikada bir: maç hatırlatma e-postaları ve zamanı gelen toplu / düzenli gönderimler.
    /// Hatalar loglanır, uygulamayı düşürmez.
    /// </summary>
    public sealed class EmailSchedulerService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EmailSchedulerService> _logger;

        public EmailSchedulerService(IServiceScopeFactory scopeFactory, ILogger<EmailSchedulerService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Uygulama açılışını bekletmesin
            try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); } catch (OperationCanceledException) { return; }
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var sent = scope.ServiceProvider.GetRequiredService<IEmailCenterService>().RunDue();
                    if (sent > 0) _logger.LogInformation("{Count} e-posta gönderim kuyruğuna alındı (hatırlatma / toplu gönderim).", sent);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "E-posta zamanlayıcı turu başarısız oldu.");
                }
                try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }
    }
}
