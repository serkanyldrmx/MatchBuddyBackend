namespace MatchBuddy.Api.Services
{
    /// <summary>
    /// Saatte bir (ayar: Media:CleanupIntervalMinutes): 24 saatten eski Pending medya (yüklenip hiçbir kayda bağlanmamış)
    /// depodan silinir, Status=Deleted. Hatalar loglanır, uygulamayı düşürmez.
    /// </summary>
    public class MediaCleanupService : BackgroundService
    {
        private static readonly TimeSpan PendingMaxAge = TimeSpan.FromHours(24);

        private readonly IMediaService _mediaService;
        private readonly ILogger<MediaCleanupService> _logger;
        private readonly TimeSpan _interval;

        public MediaCleanupService(IMediaService mediaService, ILogger<MediaCleanupService> logger, IConfiguration configuration)
        {
            _mediaService = mediaService;
            _logger = logger;
            _interval = TimeSpan.FromMinutes(Math.Max(1, configuration.GetValue("Media:CleanupIntervalMinutes", 60)));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var deleted = await _mediaService.CleanupPendingAsync(PendingMaxAge, stoppingToken);
                    if (deleted > 0) _logger.LogInformation("{Count} bekleyen (bağlanmamış) medya silindi.", deleted);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Medya temizlik turu başarısız oldu.");
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
