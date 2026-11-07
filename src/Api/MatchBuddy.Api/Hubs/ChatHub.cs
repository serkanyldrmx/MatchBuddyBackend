using MatchBuddy.DataAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace MatchBuddy.Api.Hubs
{
    /// <summary>
    /// Anlık mesajlaşma kanalı (docs/realtime-api.md). Sunucu yalnızca "değişti" sinyali gönderir; istemci veriyi
    /// mevcut REST uç noktalarından çeker (yetki kontrolleri tek yerde kalır). Kullanıcı kimliği: JWT NameIdentifier.
    /// </summary>
    [Authorize]
    public class ChatHub : Hub
    {
        public const string Path = "/hubs/chat";
        public static string MatchGroup(int matchId) => $"match:{matchId}";

        /// <summary>Maç sohbetini (soyunma odası) açan istemci o maçın yorum sinyallerine abone olur.</summary>
        public Task JoinMatch(int matchId) => Groups.AddToGroupAsync(Context.ConnectionId, MatchGroup(matchId));

        public Task LeaveMatch(int matchId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, MatchGroup(matchId));
    }

    /// <summary>REST uç noktalarından sonra ilgili kullanıcılara sinyal gönderir. Hata olursa sessizce yutulur (sinyal kritik değil).</summary>
    public sealed class RealtimeNotifier
    {
        private readonly IHubContext<ChatHub> _hub;
        private readonly ILogger<RealtimeNotifier> _logger;

        public RealtimeNotifier(IHubContext<ChatHub> hub, ILogger<RealtimeNotifier> logger)
        {
            _hub = hub;
            _logger = logger;
        }

        /// <summary>Birebir mesaj: alıcı ve gönderen (diğer cihazları) bilgilendirilir.</summary>
        public Task DirectMessageAsync(int senderId, int recipientId) => Safe(() =>
            _hub.Clients.Users(recipientId.ToString(), senderId.ToString())
                .SendAsync("message", new { kind = "user", senderId, recipientId }));

        /// <summary>Grup mesajı: grubun tüm üyeleri.</summary>
        public Task GroupMessageAsync(int groupId, int senderId) => Safe(async () =>
        {
            using var context = new MatchBuddyContext();
            var members = await context.GroupPlayers.AsNoTracking().Where(g => g.GroupId == groupId)
                .Select(g => g.PlayerId.ToString()).ToListAsync();
            if (members.Count == 0) return;
            await _hub.Clients.Users(members).SendAsync("message", new { kind = "group", groupId, senderId });
        });

        /// <summary>Maç sohbeti (yorum eklendi / silindi): o maçı açık tutan herkes.</summary>
        public Task MatchCommentAsync(int matchId) => Safe(() =>
            _hub.Clients.Group(ChatHub.MatchGroup(matchId)).SendAsync("matchComment", new { matchId }));

        /// <summary>Yeni bildirim(ler): zil anında güncellensin.</summary>
        public Task NotificationAsync(IEnumerable<int> playerIds) => Safe(() =>
            _hub.Clients.Users(playerIds.Select(id => id.ToString()).ToList()).SendAsync("notification", new { }));

        private async Task Safe(Func<Task> send)
        {
            try { await send(); }
            catch (Exception ex) { _logger.LogWarning(ex, "Anlık bildirim gönderilemedi."); }
        }
    }
}
