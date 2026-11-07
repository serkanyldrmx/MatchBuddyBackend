using MatchBuddy.Business.Abstract;

namespace MatchBuddy.Api.Services
{
    /// <summary>
    /// Periyodik olarak (varsayılan 10 dakikada bir, ayar: Reminders:IntervalSeconds)
    /// 24 saat içinde başlayacak maçların onay vermemiş asil oyuncularına hatırlatma bildirimi gönderir.
    /// Hatalar loglanır, uygulamayı asla düşürmez.
    /// </summary>
    public class MatchReminderService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MatchReminderService> _logger;
        private readonly TimeSpan _interval;

        public MatchReminderService(IServiceScopeFactory scopeFactory, ILogger<MatchReminderService> logger, IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            var seconds = configuration.GetValue("Reminders:IntervalSeconds", 600);
            _interval = TimeSpan.FromSeconds(Math.Max(1, seconds));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Maç hatırlatma servisi başladı (aralık: {Interval}).", _interval);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Her turda yeni scope (DAL her çağrıda yeni MatchBuddyContext açar)
                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IMatchParticipationService>();
                    var sent = service.SendDueReminders();
                    if (sent > 0)
                    {
                        _logger.LogInformation("{Count} oyuncuya maç hatırlatması gönderildi.", sent);
                    }

                    // Güvenilirlik: "gelmedi" penceresi kapanan maçlara katılım puanı (docs/reliability-api.md)
                    var settled = scope.ServiceProvider.GetRequiredService<MatchBuddy.DataAccess.Concrete.EntityFramework.IReliabilityDal>().SettleDue();
                    if (settled > 0)
                    {
                        _logger.LogInformation("{Count} maçın güvenilirlik puanları işlendi.", settled);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Maç hatırlatma turu başarısız oldu.");
                }

                try
                {
                    await Task.Delay(_interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
