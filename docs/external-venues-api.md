# Harici Saha (Anlaşmasız Yer) ve Mesajlaşma Güncellemesi — API Sözleşmesi

> Hata gövdeleri standarttır: `{ success:false, code, message, errors?, traceId }` (bkz. [platform-api.md](platform-api.md)).
> Migration: `20261111214150_ExternalVenuesAndChatReadState`.

## 1. Harici saha

Oyuncular artık sistemde üyesi olmayan bir sahada da maç kurabilir: sahayı **telefonla kendileri ayarlar**, uygulama
yalnızca maçı, kadroyu ve sohbeti yönetir.

- Saha, `Stadiums` tablosunda kurucuya ait bir kayıt olarak tutulur: `IsExternal = 1`, `IsListed = 0`,
  `CreatedByPlayerId = kurucu`, işletmesi / sahası / fiyat kuralı yoktur. Tesis listelerinde, admin işletme
  listesinde, OSM eşleştirmesinde ve "fırsat" kitlesinde görünmez.
- Maç doğrudan **onaylı** (`isActive = 2`) açılır; işletme onayı yoktur. `PitchId` boştur.
- Haftalık tekrar (`repeatWeekly`) harici sahada desteklenmez (seri bir sahaya bağlıdır).
- Tesis yorumu yapılamaz (`RESERVATION_REVIEW_NOT_VENUE_MATCH`).
- Kurucu aynı adla tekrar saha eklerse (büyük/küçük harf duyarsız) eski kayıt kullanılır.

### POST /api/Match/SaveExternalMatch

```json
{
  "matchName": "Perşembe akşam maçı",
  "description": "",
  "userCount": 14,
  "matchDate": "2026-11-20T21:00:00",
  "durationMinutes": 60,
  "externalVenueId": null,
  "venueName": "Yıldız Halı Saha",
  "address": "Atatürk Cad. No:5",
  "city": "İstanbul",
  "district": "Kadıköy",
  "phone": "0216 000 00 00",
  "location": "40.99, 29.03",
  "totalPrice": 2400,
  "homeTeamId": null,
  "awayTeamId": null,
  "lookingForOpponent": false,
  "minReliability": null
}
```

- `externalVenueId`: önceki harici sahalarımdan biri (bkz. `MyExternalVenues`). Verilirse `venueName` gerekmez;
  gönderilen adres / telefon / konum o kaydı günceller.
- `venueName` 2–100 karakter, `location` `"lat, lon"` (isteğe bağlı), `totalPrice` 0–100000 (isteğe bağlı; kişi başı
  payı `perPlayer` buradan hesaplanır).
- `matchDate` geçmişte olamaz, en fazla `booking.defaultMaxAdvanceDays` gün ileride olabilir.
- `durationMinutes` `venue.allowedDurations` içinden.
- `userCount` en az `2 × venue.bookingMinPlayersPerSide`, en çok `venue.playersPerSideMax × 2 + venue.extraPlayersAllowed`.
- Takım kuralları normal maç kurmayla aynıdır.

Yanıt: `{ success, message, matchId, status: 2 }`

Hata kodları: `EXTERNAL_VENUE_NAME_REQUIRED`, `EXTERNAL_VENUE_NOT_FOUND`, `EXTERNAL_DATE_REQUIRED`,
`EXTERNAL_PAST_DATE`, `EXTERNAL_TOO_FAR_AHEAD`, `EXTERNAL_INVALID_PRICE`, `VENUE_INVALID_DURATION`,
`VENUE_INVALID_LOCATION_FORMAT`, `MATCH_NAME_INVALID`, `VALIDATION_OUT_OF_RANGE`, `RESERVATION_*` (takım).

### POST /api/Match/UpdateExternalMatch

Kurucu (ya da admin) saat / süre / ücret / saha bilgisini değiştirebilir. Gönderilmeyen alanlar korunur. Saat ya da
saha adı değişirse kadrodaki oyunculara bildirim gider ve hatırlatma yeni saate göre tekrar gönderilir.

```json
{ "matchId": 42, "matchDate": "2026-11-20T22:00:00", "durationMinutes": 90, "totalPrice": 2600,
  "venueName": null, "address": null, "phone": null, "location": null }
```

Hata kodları: `EXTERNAL_NOT_EXTERNAL_MATCH`, `EXTERNAL_MATCH_CLOSED`, `MATCH_ORGANIZER_ONLY` + yukarıdakiler.

### GET /api/Match/MyExternalVenues

`[{ stadiumId, name, address, city, district, phone, location, matchCount, lastMatchDate }]` — son kullanılan önce.

### Maç DTO'larında `venue`

Maç listesi / detayındaki `venue` nesnesine eklenen alanlar: `isExternal`, `address`, `city`, `district`, `phone`,
`location`. Harici sahada `pitchId`, `pitchName`, `coverPhotoUrl` boştur ve tesis sayfası (`/venues/:id`) yoktur.

## 2. Mesajlaşma

- Mesaj metni en fazla **1000** karakter (önceden veritabanı 250'de kesiyordu). Aşılırsa `MESSAGE_TOO_LONG`.
- `sendingDate` artık sunucu saatidir (istemcinin gönderdiği değer yok sayılır).

### Okundu bilgisi

- `Messages.ReadAt`: alıcının konuşmayı açtığı an. `GET /api/Message/GetMessageById` yanıtındaki her mesajda `readAt` var.
- `POST /api/Message/MarkRead { playerId }` → o kişiden gelen okunmamışları okundu yapar. Karşı tarafa SignalR
  `messageRead { kind:"user", readerId, otherId }` gider. Bir mesaja yanıt yazmak da okundu sayılır.
- `GroupPlayers.LastReadAt`: üyenin grup sohbetini son açtığı an. `POST /api/Groups/MarkRead { groupId }`.
- `GET /api/Message/Unread` → `{ direct, groups, total }` (mesaj simgesindeki rozet).
- Migration mevcut tüm mesajları okunmuş sayar.

### GET /api/Social/MessageContacts

Her kişiye `unreadCount` eklendi; `lastMessage.read` benim gönderdiğim son mesajın görülüp görülmediğini söyler.

### Gruplar

- `GET /api/Groups/MyGroups` → `[{ groupId, groupName, createdByPlayerId, memberCount, members: PlayerSummary[],
  lastMessage: { text, sendingDate, mine, hasImage, senderName }, unreadCount }]`, son mesaja göre sıralı.
- `POST /api/Groups/SaveGroup { groupName, playerList }` → `{ success, groupId }`. Ad 1–50 karakter; engelli,
  silinmiş ve işletme hesapları eklenmez (`GROUP_NO_PLAYERS`). Kurucu `createdByPlayerId` olarak saklanır.
- `POST /api/Groups/AddMembers { groupId, playerList }` → her üye ekleyebilir.
- `POST /api/Groups/RemoveMember { groupId, playerId }` → yalnızca kurucu / admin (`GROUP_CREATOR_ONLY`).
- `POST /api/Groups/Leave { groupId }` → son üye ayrılırsa grup ve mesajları silinir.
- Üyelik değişince üyelere SignalR `message { kind:"groupChanged", groupId }` gider.
- `GetGroupMessageById` yanıtına `groupMessageId`, `playerName`, `playerSurname`, `profilePictureUrl` eklendi.
