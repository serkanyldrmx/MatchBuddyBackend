# İşletme büyüme araçları

Amaç: işletmeye "uygulama boş saatini dolduruyor, son dakika iptal kaybını azaltıyor" diyebilmek. Dört parça var.

## 1. Boş saat fırsatı ("Son dakika fırsatı")
İşletme takvimde **Boş saat ilanı** der: saha, tarih, başlangıç, süre (60/90/120 dk) ve indirim (%10–40) seçer.
- O gün ve o saat aralığı için tek günlük bir kampanya (`Campaigns.IsDeal = 1`) oluşur. Oyuncu o saati uygulamadan kurarken
  indirimli fiyatı görür (fiyat hesabındaki kampanya kuralıyla aynı).
- Bildirim (`NotificationType.SlotDeal = 15`): önce bu tesiste, sonra aynı ilçedeki tesislerde son
  `venue.dealAudienceDays` (180) gün içinde oynamış oyunculara; en fazla `venue.dealMaxRecipients` (300). Oyuncu
  Ayarlar → Bildirimler → **Saha fırsatları** ile kapatabilir (`PlayerSettings.NotifyDeals`). Telefona bildirim açıksa
  telefona da gider; dokununca tesis sayfası açılır.
- Saat şu an rezerve edilebilir olmalı (açık, boş, ön bildirim süresine uygun), en fazla `venue.dealMaxDaysAhead` (7) gün
  sonrası, gece yarısını geçemez. Günde en fazla `venue.dealMaxPerDay` (3) ilan.

| Yöntem | Yol | Gövde / yanıt |
|---|---|---|
| GET | `/api/Owner/SlotDeal/Audience?stadiumId=` | `{ count, sentToday, dailyLimit }` |
| POST | `/api/Owner/SlotDeal` | `{ pitchId, start, durationMinutes, discountPercent }` → `{ success, message, campaignId, notified }` |

Hata kodları: `OWNER_DEAL_LIMIT`, `OWNER_DEAL_NOT_FUTURE`, `OWNER_DEAL_TOO_FAR`, `OWNER_DEAL_CROSSES_MIDNIGHT`, saat uygun değilse
`VENUE_*` (dolu / kapalı / ön bildirim). `CampaignDto.isDeal` ilanları kampanya listesinde ayırt eder.

## 2. Rezervasyon onayında kurucunun güvenilirliği
`GET /api/Owner/Reservations` her satırda `creatorReliability` döner:
`{ score, level, completedMatches, lateCancels, noShows, pastReservations, organizerLateCancels }`
- `lateCancels`, `noShows`: oyuncu olarak son 10 maçında (docs/reliability-api.md).
- `pastReservations`: kurucu olarak son 180 günde gerçekleşen (onaylı, geçmiş) rezervasyon sayısı.
- `organizerLateCancels`: kurucu olarak son 180 günde son dakika iptal ettiği maçlar.
Talepler listesinde puan rozeti, rezervasyon detayında özet ve "Dikkat" uyarısı gösterilir.

## 3. Haftalık sabit maç (uygulama rezervasyonu)
Oyuncu maçı kurarken **Her hafta tekrarla** seçer (`SaveMatch.repeatWeekly = true`). `MatchSeries` satırı oluşur.
- `MatchReminderService` her turda `MatchSeriesService.Run()` çalıştırır: `NextDate`, `venue.seriesLeadDays` (7) gün içine
  girince aynı saha/saat için yeni maç açılır. Şablon serinin en son maçıdır (ad, kapasite, takımlar, kamera, min. güvenilirlik).
- İşletme serinin bir maçını onayladıysa yeni haftalar **otomatik onaylı** açılır, onaylamadıysa normal talep olarak gelir.
  İlk maç reddedilirse seri durur.
- Geçen haftanın asil/yedek oyuncularına "bu hafta da geliyor musun?" maç daveti gider (kabul = katılım).
- Saat o hafta doluysa / kapalıysa o hafta atlanır, kurucuya bildirim gider; seri devam eder.
- Bir haftanın maçı silinirse seri etkilenmez (sonraki hafta yine açılır).
- **Tekrarı durdur:** `POST /api/Match/StopRepeat { matchId }` — kurucu, tesisin işletmesi ya da admin. Oluşmuş haftalar kalır.
- `GetMatchDetail.repeatWeekly`, `Reservations[].repeatWeekly`: seri aktif mi.

## 4. Telefonla gelen rezervasyon (hızlı giriş)
- **Önceki müşteriler:** `GET /api/Owner/Customers?stadiumId=&q=` → `[{ name, phone, bookings, lastAt, lastPrice, lastTitle }]`
  (en fazla 10, ad ya da telefonda arama). Form müşteri adı yazılırken önerir; seçince telefon (ve boşsa başlık) dolar.
- **Haftalık tekrar:** `POST /api/Owner/Blocks/Save` yeni harici rezervasyonda `repeatWeeks` (2–`venue.repeatMaxWeeks`, 26).
  İlk hafta normal kurallarla kaydedilir; sonraki haftalardan dolu olanlar atlanır, mesajda sayısı yazar. Kayıtlar ortak
  `seriesId` taşır (`SlotBlockDto` / takvim öğesi).
- **Seriyi silme:** `POST /api/Owner/Blocks/Delete { slotBlockId, allFollowing: true }` bu ve sonraki haftaları siler.
- Hata: `OWNER_REPEAT_ONLY_NEW_MANUAL` (düzenlemede ya da kapalı blokta tekrar seçilemez).

Migration: `VenueGrowth` (MatchSeries tablosu, Matchs.SeriesId, SlotBlocks.SeriesId, Campaigns.IsDeal/DealSentAt/
DealNotifiedCount, PlayerSettings.NotifyDeals — mevcut oyuncular için açık).
