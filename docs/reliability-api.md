# Güvenilirlik puanı — tasarım ve sözleşme

Sahadan gelen ihtiyaç: (1) son dakika iptal edenler ve gelmeyenler maçı ve tesisi zor durumda bırakıyor;
(2) gerçek bir mazereti olan (ör. taziye) oyuncu, o an **hızlıca** uygulamaya girip maçı iptal edebilmeli ve
haksız ceza almamalı. Kurucu, oyuncuyu kabul ederken bu puana bakabilmeli. **Basit kalmalı.**

## Puan
- `Players.ReliabilityScore` int 0–100, yeni oyuncu 100. Gösterim seviyesi:
  - Tamamlanan maç sayısı < `Reliability.NewPlayerMatchCount` (3) → **Yeni oyuncu** (gri)
  - ≥ `Reliability.LevelGood` (90) → **Güvenilir** (yeşil)
  - ≥ `Reliability.LevelMedium` (70) → **Orta** (sarı)
  - aksi → **Riskli** (kırmızı)
- Hareket tablosu `ReliabilityEvents`: PlayerId, MatchId?, Type, Delta, ReasonCode?, Note?, CreatedAt,
  ReversedAt?, ReversedByPlayerId?. Puan = 100 + Σ(geri alınmamış Delta), 0–100 arasında sınırlanır
  (tek kaynak: olaylar; Players.ReliabilityScore önbellek kolonu).

| Olay (Type) | Ne zaman | Varsayılan Delta (parametre) |
|---|---|---|
| Attended | Maç tamamlandı (sonuç girildi / maç saati + süre geçti), oyuncu Going ve "gelmedi" işaretlenmedi | +2 (`Reliability.AttendBonus`) — 100'ü geçmez |
| LateCancel | Maça < `Reliability.LateCancelHours` (24) saat kala iptal | −5 (`Reliability.LateCancelPenalty`) |
| VeryLateCancel | Maça < `Reliability.VeryLateCancelHours` (3) saat kala ya da başladıktan sonra iptal | −10 (`Reliability.VeryLateCancelPenalty`) |
| NoShow | Kurucu maçtan sonra `Reliability.NoShowMarkWindowHours` (48) içinde "gelmedi" işaretler | −20 (`Reliability.NoShowPenalty`) |
| OrganizerLateCancel | Kurucu maçı < 24 saat kala iptal eder (tesis reddi / tesis iptali hariç) | −10 (`Reliability.OrganizerLateCancelPenalty`) |

24 saatten önce iptal → ceza yok (olay yazılmaz). Bekleme listesindeki (Waitlist) oyuncunun çıkması ceza değildir.
Tüm sayılar `SystemParameters` (grup `Reliability`) içindedir; admin panelinden değişir.

## Hızlı iptal ("Maça gelemiyorum")
- Oyuncunun katıldığı yaklaşan maç her yerde tek dokunuşla iptal edilebilir: ana sayfadaki "Sıradaki maçın" kartı,
  Takvimim, maç detayı ve hatırlatma bildirimi.
- Açılan küçük pencere: mazeret seçimi (çip): **Acil durum / Sağlık / Vefat–taziye / İş / Ulaşım / Diğer** (+ isteğe bağlı kısa not),
  ve ceza önizlemesi: "Maça 2 saat kaldı. Güvenilirlik puanından 10 düşecek; kurucu mazeretini kabul ederse geri alınır." → **İptal et**.
- Sonuç: katılım Declined; bekleme listesinden ilk kişi otomatik Going'e alınır (mevcut kural) ve bildirim alır;
  kurucuya "X maçtan çekildi (mazeret: Vefat–taziye)" bildirimi gider — **Mazereti kabul et** aksiyonuyla.
- Kurucu mazereti kabul ederse olay geri alınır (ReversedAt) → puan iade. Kurucu kendisi için kabul edemez.

## Kurucu tarafı
- Katılımcı listeleri, bekleme listesi, onay bekleyenler, oyuncu profili ve davet önerilerinde puan rozeti (seviye rengi + sayı)
  ve kısa özet: son 10 maç → geldi / geç iptal / gelmedi sayıları.
- Maç oluştururken (Gelişmiş, kapalı bölüm) isteğe bağlı **Minimum güvenilirlik** (yok / 70 / 90). Ayarlıysa altındaki oyuncular
  doğrudan katılamaz: katılım **Pending** olur, kurucu "Kabul / Reddet" der (bildirimle). Yeni oyuncular bu kurala takılmaz.
- Maç sonrası (sonuç girme ekranı) "Gelmeyenler" işaretleme: katılımcı listesinde onay kutuları; misafir oyuncular hariç.

## Uç noktalar (öneri — uygulayan ajan netleştirip bu dosyayı günceller)
- `POST /api/Match/Leave` → gövdeye `reasonCode?`, `note?` eklenir; dönüşte `{ penalty, newScore }`.
- `GET /api/Match/LeavePreview?matchId=` → `{ hoursLeft, penalty, kind: "none|late|veryLate" }`
- `POST /api/Match/MarkNoShows` `{ matchId, playerIds[] }` (kurucu, pencere içinde; tekrar çağrı = güncelleme)
- `POST /api/Match/AcceptExcuse` `{ matchId, playerId }` (kurucu)
- `POST /api/Match/RespondJoin` `{ matchId, playerId, accept }` (Pending onayı)
- `GET /api/Players/Reliability?playerId=` → `{ score, level, completedMatches, last10: { attended, lateCancels, noShows }, history[] (sadece kendisi veya admin) }`
- Oyuncu özetlerinin döndüğü yerlerde (participants, profil, arama) `reliability: { score, level }` alanı.
- Hata kodları `docs/platform-api.md` kataloğuna (RELIABILITY_*, MATCH_*) eklenir.
