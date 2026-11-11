# Etkileşim Paketi — Mesaj Tepkileri, Hava Durumu, Davet Linki, Satış Fırsatları

> Hata gövdeleri standarttır: `{ success:false, code, message, errors?, traceId }` (bkz. [platform-api.md](platform-api.md)).
> Migration: `EngagementFeatures`. Önceki paket: [external-venues-api.md](external-venues-api.md).

## 1. Mesaj yanıtı, tepki, silme, "yazıyor…"

### Konuşma yanıtı (değişti)
`POST /api/Message/GetMessageById` ve `POST /api/GroupsMessage/GetGroupMessageById` artık `ChatMessageDto[]` döner:

```json
{ "messageId": 10, "groupMessageId": null, "groupId": null, "matchMessage": "Geliyorum!", "sendingDate": "…",
  "sendPlayerId": 2, "recipientPlayerId": 3, "imageUrl": null, "readAt": "…", "status": 1, "isDeleted": false,
  "userName": "…", "playerName": "…", "playerSurname": "…", "profilePictureUrl": "…",
  "replyTo": { "id": 9, "sendPlayerId": 3, "senderName": "Ali Yılmaz", "text": "Geliyor musun?", "hasImage": false, "isDeleted": false },
  "reactions": [{ "emoji": "⚽", "count": 2, "mine": true, "names": ["Ali Yılmaz", "Cem Demir"] }] }
```

Silinmiş mesajda `matchMessage`, `imageUrl`, `replyTo` boş, `reactions` boş listedir.

### Yanıt
`MessageSave` gövdesine `replyToMessageId`, `SaveGroupMessage` gövdesine `replyToGroupMessageId` eklenir.
Yanıtlanan mesaj aynı konuşmada / grupta olmalı, yoksa `MESSAGE_REPLY_INVALID`.

### Tepki
`POST /api/Message/React { messageId | groupMessageId, emoji }`
- Oyuncu başına mesaj başına tek tepki. Aynı emoji ya da `emoji: null` → kaldırır; farklı emoji → değiştirir.
- İzinli emojiler (`GET /api/Message/Reactions`): 👍 ❤️ 😂 🔥 😮 😢 👏 💪 ⚽ 🥅 🧤 🏆 🟨 🟥 🍻. Diğerleri `MESSAGE_INVALID_REACTION`.
- Konuşmanın tarafı / grubun üyesi değilse `MESSAGE_NOT_FOUND`; silinmiş mesajda `MESSAGE_ALREADY_DELETED`.

### Silme
`POST /api/Message/Delete { messageId | groupMessageId }` — yalnızca gönderen (`MESSAGE_NOT_YOURS`). Metin ve resim
boşaltılır, tepkiler silinir, konuşmada "Bu mesaj silindi" kalır. Konuşma listesindeki son mesaj önizlemesi de
"🚫 Bu mesaj silindi" olur; silinen mesaj okunmamış sayısına girmez.

Tepki ve silme sonrası iki tarafa (grupta tüm üyelere) SignalR `message` sinyali gider.

### "Yazıyor…" (SignalR hub metotları)
- `Typing(recipientId)` → alıcıya `typing { kind:"user", senderId }`
- `TypingGroup(groupId)` → diğer üyelere `typing { kind:"group", groupId, senderId }` (yalnızca üye çağırabilir)

Kaydedilmez. İstemci en fazla 3 sn'de bir çağırır, gelen sinyali ~4,5 sn gösterir.

## 2. Hava durumu

`GET /api/Weather/Matches?ids=12,15,20` (en fazla 60) →
`[{ matchId, available, indoor, time, temperature, apparentTemperature, precipitationProbability, precipitation, weatherCode, windSpeed, approximate }]`

- Kaynak: [Open-Meteo](https://open-meteo.com) (ücretsiz, anahtarsız). `weatherCode`: WMO kodu.
- Tahmin maç başlangıç saatine aittir (maç sürüyorsa şimdiki saat); maç 15 günden uzaksa ya da bittiyse `available:false`.
- Kapalı saha (`Pitch.IsIndoor`) → `indoor:true`, tahmin istenmez.
- Konum: sahanın `Location` alanı; yoksa ilçe/şehir adı Open-Meteo geocoding ile çözülür (`approximate:true`).
- Önbellek: konum başına 1 saat, geocoding 7 gün. Servis yanıt vermezse `available:false` (5 dk sonra tekrar denenir).
- Yapılandırma: `Weather:TimeZone` (varsayılan `Europe/Istanbul`).

## 3. Davet linki (WhatsApp)

- `POST /api/Match/ShareLink { matchId }` → `{ code, url, text }`. Kod (8 karakter) ilk istekte oluşur, sonra aynı kalır
  (`Matchs.ShareCode`). `text` WhatsApp'a hazırdır: maç adı, tarih, saha, eksik kişi sayısı ve link.
- Link: `{Share:BaseUrl}/m/{kod}` (`Share:BaseUrl` yoksa `Auth:FrontendBaseUrl`).
- `GET /m/{kod}` ve `GET /share/m/{kod}` (anonim) → Open Graph etiketli HTML: önizleme kartında
  "⚽ Maç adı · 3 kişi eksik", tarih, saha, kişi başı ücret ve `og-match.png` görünür; tarayıcı `/davet/{kod}`'a yönlenir.
  Vercel'de `vercel.json` içindeki `/m/:code` kuralı bu adrese yönlendirir.
- `GET /api/Public/Match/{kod}` (anonim) → `PublicMatchInviteDto`: maç adı, tarih, süre, saha, ilçe, kapasite,
  katılan / eksik sayısı, kişi başı ücret, kurucu adı, ilk 10 oyuncunun adı + soyadının baş harfi + fotoğrafı.
  Geçersiz kod: 404 `SHARE_NOT_FOUND`.
- `POST /api/Match/JoinByCode { code }` → normal katılma kuralları (`Join`): `{ success, matchId, status, message }`.

Ön yüz: `/davet/:code` (ve `/m/:code`) giriş gerektirmez. Girişli oyuncu "Kadroya katıl"a basar; girişsiz kullanıcı
kayıt / girişe yönlenir, kod `localStorage['mb.pendingInvite']`'ta saklanır, girişten sonra davete dönülüp otomatik katılınır.

## 4. Harici sahalardan satış fırsatları (admin)

- `GET /api/Admin/ExternalVenueLeads?q=` → aynı ad + şehirdeki harici sahalar tek satır:
  `{ key, name, city, district, phones[], address, location, stadiumIds[], addedBy, matchCount, upcomingCount, playerCount, firstMatchDate, lastMatchDate }`,
  en çok maç yapılan önce. Bağlanmış sahalar listede yoktur.
- `GET /api/Admin/ExternalVenueLeads/Targets?q=` → bağlanabilecek tesisler `{ stadiumId, name, city, district, hasOwner }`.
- `POST /api/Admin/ExternalVenueLeads/Link`
  - `{ stadiumIds, targetStadiumId }` → mevcut tesise bağla
  - `{ stadiumIds, createCandidate: true, name, city, district, address, phone, location }` → yeni aday tesis oluştur
    (işletmesi olmadığı için oyunculara görünmez; admin işletme hesabı verince yayına girer)
  - Seçilen harici sahalardaki tüm maçlar hedef tesise taşınır, harici kayıtlara `LinkedStadiumId` yazılır.
    Oyuncu sonra aynı harici sahayı seçerse maç doğrudan bağlı tesiste açılır.
  - Hata: `EXTERNAL_LINK_NO_VENUES`, `EXTERNAL_LINK_TARGET_INVALID`.
