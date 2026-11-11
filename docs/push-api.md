# Telefona bildirim (Web Push) ve PWA

Mobil uygulama yazmadan, site telefona "uygulama gibi" eklenir (PWA) ve uygulama kapalıyken de bildirim alınır.
Android / masaüstü Chrome, Edge, Firefox: doğrudan. iPhone (iOS 16.4+): önce Safari'de Paylaş → "Ana Ekrana Ekle",
sonra ana ekrandaki MatchBuddy'den açıp Ayarlar → "Telefona bildirim".

## Akış
1. Oyuncu Ayarlar → Bildirimler → **Telefona bildirim** anahtarını açar; tarayıcı izin ister.
2. Frontend (`src/pwa.js`) service worker (`public/sw.js`) ile abone olur, aboneliği `POST /api/Push/Subscribe` ile kaydeder.
3. Uygulamadaki **her bildirim** zaten `NotificationWriter` ile yazılıyor (bildirim tercihleri orada uygulanır).
   `PushDispatchService` 5 sn'de bir yeni `PlayerNotification` satırlarını (`PushedAt IS NULL`) alır:
   - açık uygulamalara SignalR `notification` sinyali (zil anında yenilenir, docs/realtime-api.md),
   - aboneliği olan cihazlara Web Push: `{ title, body, url, tag }`. Dokununca `url` açılır
     (davetler → `/invites`, maçla ilgili → `/match-details/{id}`, diğerleri → `/NotificationList`).
4. Cihaz aboneliği bıraktıysa (404/410) abonelik silinir. Çıkışta bu cihazın aboneliği, "Tüm cihazlardan çıkış"ta
   oyuncunun tüm abonelikleri silinir.

## Uç noktalar
| Yöntem | Yol | Kim | Gövde / yanıt |
|---|---|---|---|
| GET | `/api/Push/PublicKey` | anonim | `{ publicKey }` (VAPID) |
| POST | `/api/Push/Subscribe` | oyuncu | tarayıcının `PushSubscription.toJSON()` çıktısı: `{ endpoint, keys: { p256dh, auth } }` |
| POST | `/api/Push/Unsubscribe` | oyuncu | `{ endpoint }` |

## Yapılandırma (`appsettings.json` → `WebPush`)
| Anahtar | Açıklama |
|---|---|
| `PublicKey`, `PrivateKey` | VAPID anahtarları. Boşsa ilk açılışta üretilip `App_Data/vapid.json`'a yazılır (repo dışında). **Canlıda sabit anahtar verin**; anahtar değişirse kullanıcılar bildirimi yeniden açmalı. Üretmek için: `npx web-push generate-vapid-keys` |
| `Subject` | İletişim adresi, ör. `mailto:destek@alanadi.com` |
| `IntervalSeconds` | Kuyruk tarama aralığı (varsayılan 5) |

Notlar: Web Push yalnızca HTTPS (ve geliştirmede `localhost`) üzerinde çalışır. Migration `WebPush` mevcut bildirimleri
gönderilmiş sayar; servis açılışta 1 saatten eski bekleyenleri de göndermeden işaretler (telefonlara eski bildirim yağmaz).
