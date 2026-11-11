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

## Uç noktalar (uygulandı)

| Yöntem | Yol | Kim | Gövde / yanıt |
|---|---|---|---|
| POST | `/api/Match/Leave` | oyuncu | `{ matchId, reasonCode?, note? }` → `{ success, message, penalty, newScore }`. `reasonCode`: emergency, health, bereavement, work, transport, other |
| GET | `/api/Reliability/LeavePreview?matchId=` | oyuncu | `{ hoursLeft, kind: none\|late\|veryLate, penalty, currentScore, isGoing }` |
| POST | `/api/Reliability/MarkNoShows` | kurucu/admin | `{ matchId, playerIds[] }` — tam liste, tekrar çağrı günceller. Maç başladıktan sonra, pencere içinde |
| POST | `/api/Reliability/AcceptExcuse` | kurucu/admin | `{ matchId, playerId }` — bu maçtaki iptal/gelmedi cezaları geri alınır |
| GET | `/api/Reliability/Player?playerId=` | herkes | `{ score, level, completedMatches, attended, lateCancels, noShows, history? }` (history yalnızca kendisi/admin) |
| GET | `/api/Reliability/Withdrawals?matchId=` | kurucu/admin | Son dakika çekilenler ve gelmeyenler |

`GET /api/Match/GetMatchDetail` ek alanlar: `participants[].reliability { score, level, completedMatches }`, `participants[].noShow`,
`minReliability`, `noShowWindowOpen`, `withdrawals` (yalnızca kurucu/admin).

Maç oluşturma (`POST /api/Match/SaveMatch`): `minReliability` (1–100, boş = sınır yok). Sınırın altındaki oyuncu katılmaya çalışırsa
`RELIABILITY_TOO_LOW` (400). Yeni oyuncular ve davetle / rakip takım olarak katılanlar sınıra takılmaz. (Tasarımdaki "Pending + kurucu onayı"
akışı sadelik için uygulanmadı; sınır doğrudan engeller.)

Kurucu, başkalarının "katılıyor" olduğu maçı `LateCancelHours` içinde silerse kendisine `OrganizerLateCancel` yazılır.

Arka plan: `MatchReminderService` her turda `SettleDue` çalıştırır — "gelmedi" penceresi kapanan maçlarda katılan ve işaretlenmeyen
oyunculara `Attended` yazılır (maç saatine tarihlenir), maç `ReliabilitySettledAt` ile işaretlenir.

Hata kodları: `RELIABILITY_INVALID_REASON`, `RELIABILITY_NOTE_TOO_LONG`, `RELIABILITY_TOO_LOW`, `RELIABILITY_NOSHOW_NOT_STARTED`,
`RELIABILITY_NOSHOW_WINDOW_CLOSED`, `RELIABILITY_NOSHOW_NOT_PARTICIPANT`, `RELIABILITY_NO_PENALTY`, `RELIABILITY_CANNOT_EXCUSE_SELF`,
`MATCH_ORGANIZER_ONLY`.

## Yedek bul (son dakika boşalan yer)
Biri asil kadrodan çekildiğinde yedek listesindeki ilk oyuncu bugünkü gibi **otomatik** asile geçer. Yedek listesi boşsa ve maçta
boş yer kalmışsa kurucu "Yedek bul" der: adaylar listelenir, **kimlere haber verileceğini kurucu seçer**. Seçilenlere maç daveti gider
("yer açıldı" bildirimi, telefona bildirim açıksa telefona da). Daveti ilk kabul eden kadroya girer; sonra kabul edenler yedeğe düşer.
Davetle katılım minimum güvenilirlik sınırına takılmaz (kurucu kendisi seçti).

Adaylar: kurucunun son `reliability.substituteLookbackDays` (180) gün içinde birlikte oynadığı oyuncular + takip ettikleri.
Kadrodakiler (asil/yedek), işletmeler, silinmiş ve engelli hesaplar çıkarılır. Sıra: güvenilirlik puanı (yeni oyuncular sonda),
sonra birlikte oynanan maç sayısı. En fazla `reliability.substituteCandidateLimit` (30) aday.

| Yöntem | Yol | Kim | Gövde / yanıt |
|---|---|---|---|
| GET | `/api/Invites/SubstituteCandidates?matchId=` | kurucu/admin | `{ openSpots, candidates: [{ playerId, playerName, playerSurname, userName, profilePictureUrl, reliability, matchesTogether, invited }] }` |
| POST | `/api/Invites/CallSubstitutes` | kurucu/admin | `{ matchId, playerIds[] }` → `{ success, message, invited, skipped }`. Kabul: mevcut `POST /api/Invites/Respond` |

Hata kodları: `MATCH_ORGANIZER_ONLY` (403), `INVITE_NO_OPEN_SPOT` (maç dolu), `INVITE_NO_PLAYERS`, `INVITE_BATCH_LIMIT`.
