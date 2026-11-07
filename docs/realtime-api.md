# Realtime API: anlık mesajlaşma (SignalR)

Hub: `/hubs/chat` (SignalR, JWT gerekli). WebSocket bağlantısında token `?access_token=` ile gönderilir; API bunu yalnızca hub yolunda kabul eder.
İstemci: `@microsoft/signalr` — `src/api/realtime.js`.

Sunucu yalnızca **"değişti" sinyali** yollar; istemci veriyi mevcut REST uç noktalarından çeker. Böylece yetki ve engelleme kuralları tek yerde kalır.

| Olay | Kime | Yük | Tetikleyen |
|---|---|---|---|
| `message` | alıcı + gönderen | `{ kind: "user", senderId, recipientId }` | `POST /api/Message/MessageSave` |
| `message` | grubun tüm üyeleri | `{ kind: "group", groupId, senderId }` | `POST /api/GroupsMessage/SaveGroupMessage` |
| `matchComment` | maçı açık tutanlar | `{ matchId }` | `SaveMatchComment`, `DeleteMatchComment` (maç sohbeti / soyunma odası) |

Hub yöntemleri: `JoinMatch(matchId)`, `LeaveMatch(matchId)` — maç sohbeti ekranı açıkken abone olunur.

Bağlantı kurulamazsa (proxy WebSocket'i engelliyor vb.) mesaj paneli 30 sn'de bir yoklamaya devam eder.
Ters vekil sunucu (nginx vb.) arkasında `Upgrade`/`Connection` başlıkları iletilmelidir.
