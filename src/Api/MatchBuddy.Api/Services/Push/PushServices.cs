using System.Net;
using System.Text.Json;
using MatchBuddy.Api.Hubs;
using MatchBuddy.DataAccess;
using MatchBuddy.Entities.Entity;
using Microsoft.EntityFrameworkCore;
using WebPush;
using PushSubscriptionRow = MatchBuddy.Entities.Entity.PushSubscription;

namespace MatchBuddy.Api.Services.Push
{
    /// <summary>
    /// VAPID anahtarları (docs/push-api.md). Öncelik: WebPush:PublicKey / WebPush:PrivateKey yapılandırması.
    /// Yoksa bir kez üretilip App_Data/vapid.json'a yazılır (repo dışında; aynı sunucuda kalıcı).
    /// Anahtar değişirse eski abonelikler geçersizleşir, kullanıcılar bildirimi yeniden açmalıdır.
    /// </summary>
    public sealed class VapidKeys
    {
        public string PublicKey { get; }
        public string PrivateKey { get; }
        public string Subject { get; }

        public VapidKeys(IConfiguration configuration, IWebHostEnvironment env, ILogger<VapidKeys> logger)
        {
            Subject = configuration["WebPush:Subject"] is { Length: > 0 } s ? s : "mailto:destek@matchbuddy.app";
            var pub = configuration["WebPush:PublicKey"];
            var priv = configuration["WebPush:PrivateKey"];
            if (!string.IsNullOrWhiteSpace(pub) && !string.IsNullOrWhiteSpace(priv))
            {
                PublicKey = pub;
                PrivateKey = priv;
                return;
            }

            var path = Path.Combine(env.ContentRootPath, "App_Data", "vapid.json");
            try
            {
                if (File.Exists(path))
                {
                    var saved = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
                    if (saved != null && saved.TryGetValue("publicKey", out var p) && saved.TryGetValue("privateKey", out var k))
                    {
                        PublicKey = p;
                        PrivateKey = k;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "App_Data/vapid.json okunamadı; yeni anahtar üretilecek.");
            }

            var keys = VapidHelper.GenerateVapidKeys();
            PublicKey = keys.PublicKey;
            PrivateKey = keys.PrivateKey;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["publicKey"] = PublicKey,
                ["privateKey"] = PrivateKey,
            }));
            logger.LogInformation("Web Push VAPID anahtarları üretildi: {Path}. Canlı ortamda WebPush:PublicKey/PrivateKey ayarlayın.", path);
        }
    }

    /// <summary>
    /// Bildirim kuyruğu: yeni PlayerNotification satırlarını (PushedAt = null) birkaç saniyede bir alır,
    /// açık uygulamalara anlık "notification" sinyali, aboneliği olan cihazlara Web Push gönderir.
    /// Bildirimler her yerde NotificationWriter ile yazıldığı için tüm türler (yedek çağrısı dahil) buradan geçer.
    /// </summary>
    public sealed class PushDispatchService : BackgroundService
    {
        private const int Batch = 200;
        private readonly VapidKeys _keys;
        private readonly RealtimeNotifier _realtime;
        private readonly ILogger<PushDispatchService> _logger;
        private readonly TimeSpan _interval;
        private readonly WebPushClient _client = new();

        public PushDispatchService(VapidKeys keys, RealtimeNotifier realtime, ILogger<PushDispatchService> logger, IConfiguration configuration)
        {
            _keys = keys;
            _realtime = realtime;
            _logger = logger;
            _interval = TimeSpan.FromSeconds(Math.Max(1, configuration.GetValue("WebPush:IntervalSeconds", 5)));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Açılışta birikmiş eski satırlar telefona yağmasın: 1 saatten eski bekleyenler gönderilmiş sayılır
            try
            {
                using var context = new MatchBuddyContext();
                var cutoff = DateTime.Now.AddHours(-1);
                await context.PlayerNotification.Where(p => p.PushedAt == null && p.SentAt < cutoff)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.PushedAt, DateTime.Now), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Eski bildirim kuyruğu temizlenemedi.");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    while (await DispatchOnce(stoppingToken) == Batch) { }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Bildirim gönderim turu başarısız oldu.");
                }

                try { await Task.Delay(_interval, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task<int> DispatchOnce(CancellationToken ct)
        {
            using var context = new MatchBuddyContext();
            var rows = await context.PlayerNotification
                .Where(p => p.PushedAt == null)
                .OrderBy(p => p.PlayerNotificationId)
                .Take(Batch)
                .Select(p => new
                {
                    p.PlayerNotificationId, p.PlayerId, p.Notification.NotificationId, p.Notification.Text,
                    p.Notification.Type, p.Notification.MatchId, p.Notification.StadiumId,
                })
                .ToListAsync(ct);
            if (rows.Count == 0) return 0;

            // Önce işaretle: gönderim hatası aynı bildirimi tekrar tekrar yollamasın
            var ids = rows.Select(r => r.PlayerNotificationId).ToList();
            await context.PlayerNotification.Where(p => ids.Contains(p.PlayerNotificationId))
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.PushedAt, DateTime.Now), ct);

            var playerIds = rows.Select(r => r.PlayerId).Distinct().ToList();
            await _realtime.NotificationAsync(playerIds);

            var subscriptions = await context.PushSubscriptions.AsNoTracking()
                .Where(s => playerIds.Contains(s.PlayerId))
                .ToListAsync(ct);
            if (subscriptions.Count == 0) return rows.Count;

            var vapid = new VapidDetails(_keys.Subject, _keys.PublicKey, _keys.PrivateKey);
            var dead = new List<int>();
            foreach (var row in rows)
            {
                var payload = JsonSerializer.Serialize(new
                {
                    title = "MatchBuddy",
                    body = row.Text,
                    url = UrlFor(row.Type, row.MatchId, row.StadiumId),
                    tag = $"n{row.NotificationId}",
                });
                foreach (var sub in subscriptions.Where(s => s.PlayerId == row.PlayerId && !dead.Contains(s.PushSubscriptionId)))
                {
                    try
                    {
                        await _client.SendNotificationAsync(new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth), payload, vapid, ct);
                    }
                    catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
                    {
                        dead.Add(sub.PushSubscriptionId); // cihaz aboneliği bıraktı
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogWarning(ex, "Web Push gönderilemedi (abonelik {Id}).", sub.PushSubscriptionId);
                    }
                }
            }

            if (dead.Count > 0)
            {
                await context.PushSubscriptions.Where(s => dead.Contains(s.PushSubscriptionId)).ExecuteDeleteAsync(ct);
            }
            return rows.Count;
        }

        /// <summary>Bildirime dokununca açılacak sayfa (frontend rotaları).</summary>
        public static string UrlFor(NotificationType type, int? matchId, int? stadiumId = null) => type switch
        {
            NotificationType.SlotDeal when stadiumId.HasValue => $"/venues/{stadiumId.Value}",
            NotificationType.MatchInvite or NotificationType.TeamInvite => "/invites",
            NotificationType.Message => "/home",
            _ when matchId.HasValue => $"/match-details/{matchId.Value}",
            _ => "/NotificationList",
        };
    }

    /// <summary>Abonelik kaydı / silme.</summary>
    public static class PushSubscriptionStore
    {
        public static void Save(int playerId, string endpoint, string p256dh, string auth, string? userAgent)
        {
            using var context = new MatchBuddyContext();
            var row = context.PushSubscriptions.FirstOrDefault(s => s.Endpoint == endpoint);
            if (row == null)
            {
                context.PushSubscriptions.Add(new PushSubscriptionRow
                {
                    PlayerId = playerId, Endpoint = endpoint, P256dh = p256dh, Auth = auth,
                    UserAgent = userAgent, CreatedAt = DateTime.Now,
                });
            }
            else
            {
                // Aynı tarayıcıda başka hesapla giriş: abonelik yeni hesaba geçer
                row.PlayerId = playerId;
                row.P256dh = p256dh;
                row.Auth = auth;
                row.UserAgent = userAgent;
            }
            context.SaveChanges();
        }

        public static void Remove(int playerId, string endpoint)
        {
            using var context = new MatchBuddyContext();
            context.PushSubscriptions.Where(s => s.Endpoint == endpoint && s.PlayerId == playerId).ExecuteDelete();
        }

        public static void RemoveAll(int playerId)
        {
            using var context = new MatchBuddyContext();
            context.PushSubscriptions.Where(s => s.PlayerId == playerId).ExecuteDelete();
        }
    }
}
