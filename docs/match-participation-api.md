# Maç Katılımı, Sonuç, MVP ve Puanlama — API Sözleşmesi
> **Hata gövdeleri:** bu dokümandaki tüm hata yanıtları artık standart gövdeyi kullanır: `{ success:false, code, message, errors?, traceId }`. Hata kodları, yerelleştirme ve parametreler için bkz. [platform-api.md](platform-api.md). Mesaj örnekleri `message` alanının Türkçe metnidir.

Bu doküman frontend (MatcBuddyReact) ile backend (MatcBuddy) arasındaki sözleşmedir.
Tüm uç noktalar `[Authorize]`; "ben" = JWT'deki oyuncu (`User.GetPlayerId()`).
JSON alan adları camelCase döner (ASP.NET varsayılanı).

## Veri modeli

### Match (mevcut tabloya eklenecek alanlar)
| Alan | Tip | Not |
|---|---|---|
| CreatedByPlayerId | int? (FK Players, NoAction) | Maçı oluşturan. Eski maçlarda null. |
| HomeScore | int? | Sonuç girilince dolar |
| AwayScore | int? | |
| ResultEnteredAt | DateTime? | |

### MatchParticipant (yeni) — maça özel kadro
| Alan | Tip | Not |
|---|---|---|
| MatchParticipantId | int PK | |
| MatchId | int FK (Cascade) | |
| PlayerId | int FK (NoAction) | |
| Status | byte | 1 = Katılıyor (Going), 2 = Yedek (Waitlist), 3 = Gelemiyor (Declined) |
| Side | byte? | 1 = Ev sahibi (Home), 2 = Deplasman (Away), null = atanmadı |
| Confirmed | bool | Maç öncesi "geliyorum" onayı |
| JoinedAt | DateTime | Yedek sırası bu alana göre |
| ReminderSentAt | DateTime? | Hatırlatma tekrar gönderilmesin |
| Goals | int (default 0) | Sonuçla birlikte girilir |
| Assists | int (default 0) | |

Unique index: (MatchId, PlayerId).

### MatchLike (yeni)
MatchLikeId PK, MatchId FK (Cascade), PlayerId FK (NoAction), CreatedAt. Unique (MatchId, PlayerId).
`Match.Likes` sayaç olarak kalır ve toggle ile güncellenir.

### MvpVote (yeni)
MvpVoteId PK, MatchId FK (Cascade), VoterPlayerId FK (NoAction), VotedPlayerId FK (NoAction), CreatedAt. Unique (MatchId, VoterPlayerId).

### PlayerRating (yeni)
PlayerRatingId PK, MatchId FK (Cascade), RaterPlayerId FK (NoAction), RatedPlayerId FK (NoAction), Score byte (1–5), CreatedAt. Unique (MatchId, RaterPlayerId, RatedPlayerId).

### Migration ile veri taşıma
Eski maçlar için `MatchTeam` + `PlayerTeam` üzerinden `MatchParticipant` kayıtları üretilir:
maçın ilk takımı (en küçük MatchTeamId) Side = 1, ikinci takımı Side = 2, Status = 1, Confirmed = 0, JoinedAt = GETDATE().
Kapasite aşılırsa da hepsi Going kalır (geçmiş veri).

## Kurallar
- **Kapasite** = `Match.UserCount`. Going sayısı < kapasite ise katılan Going olur, değilse Waitlist.
- **Katılım kapalı:** maç tarihi geçmişse veya `IsActive == 3` (reddedildi).
- **Taraf ataması:** Going olan yeni oyuncu, daha az oyuncusu olan tarafa (eşitse Home) atanır.
- **Ayrılma:** Going bir oyuncu ayrılırsa kaydı Status = 3 olur; Waitlist'teki en eski oyuncu Going'e yükselir, boşalan tarafa atanır ve bildirim alır.
- **Yönetim yetkisi (canManage):** admin veya `CreatedByPlayerId == ben`. `CreatedByPlayerId` null olan eski maçlarda: admin veya maçta Going olan oyuncu.
- **Diziliş yetkisi (canArrange):** canManage veya maçta Going olan herhangi bir oyuncu. `ArrangeLineup` bu kuralla çalışır (maç geçmiş ya da reddedilmiş olmamalı); detay yanıtında `canArrange` alanı döner.
- **Sonuç girme:** canManage ve maç tarihi geçmiş. Tekrar girilebilir (düzeltme).
- **MVP oyu / puanlama:** sadece maç tarihi geçmiş ve oy veren Going ise. Kendine oy/puan verilemez; hedef de Going olmalı. Oy değiştirilebilir (upsert).
- **UserScore:** bir oyuncu puan aldığında `UserScore = round(ortalama(Score) * 20)` (0–100) olarak yeniden hesaplanır.
- **Bildirimler (NotificationType):** mevcut enum'a `MatchReminder = 6`, `WaitlistPromoted = 7`, `MatchResult = 8` eklenir.
  - Birisi katılınca maçın kurucusuna `MatchJoin` ("X maçına katıldı").
  - Yedekten asile geçene `WaitlistPromoted`.
  - Sonuç girilince Going oyunculara `MatchResult` ("Maç sonucu: 3-2. MVP'ni seç ve takım arkadaşlarını puanla").
  - **Hatırlatma (BackgroundService, 10 dakikada bir):** 24 saat içinde başlayacak, reddedilmemiş maçlarda Going + Confirmed=false + ReminderSentAt=null olan oyunculara `MatchReminder` gönderilir, ReminderSentAt set edilir.

## Uç noktalar (MatchController altında, `/api/Match/...`)

### GET `GetMatchFeed`
Ana sayfa listesi. Tüm maçlar (frontend filtreler).
```json
[{
  "matchId": 1, "matchName": "Perşembe Maçı", "matchDate": "2026-11-20T21:00:00",
  "description": "...", "isActive": 2, "capacity": 10, "goingCount": 7, "waitlistCount": 1,
  "likes": 4, "likedByMe": true, "myStatus": 1,            // myStatus: null | 1 | 2 | 3
  "stadiumId": 3, "stadiumName": "Bosna-2", "stadiumAddress": "...",
  "createdBy": { /* PublicPlayerDto */ } | null,
  "participantsPreview": [ /* en fazla 5 PublicPlayerDto, Going */ ],
  "homeScore": null, "awayScore": null,
  "mvp": { /* PublicPlayerDto */ } | null
}]
```

### GET `GetMatchDetail?matchId=`
```json
{
  "matchId": 1, "matchName": "...", "matchDate": "...", "description": "...", "isActive": 2,
  "capacity": 10, "likes": 4, "likedByMe": false,
  "stadium": { "stadiumId": 3, "stadiumName": "...", "address": "...", "location": "41.0, 29.0",
               "openingTime": "08:00:00", "closingTime": "23:00:00" },
  "createdBy": { /* PublicPlayerDto */ } | null,
  "canManage": true, "canArrange": true, "isPast": false,
  "myStatus": 1, "myConfirmed": false, "mySide": 1,
  "participants": [{
    "player": { /* PublicPlayerDto */ }, "status": 1, "side": 1, "confirmed": true,
    "joinedAt": "...", "goals": 0, "assists": 0, "mvpVotes": 0, "avgRating": 4.5 | null
  }],
  "result": { "homeScore": 3, "awayScore": 2, "enteredAt": "..." } | null,
  "mvp": { "player": { /* PublicPlayerDto */ }, "votes": 4 } | null,
  "myMvpVote": 12 | null,                       // oy verdiğim playerId
  "myRatings": [{ "playerId": 12, "score": 4 }] // benim verdiğim puanlar
}
```
`participants` Status 1 ve 2 olanları içerir (3 = gelemiyor olanlar listelenmez). Waitlist JoinedAt'e göre sıralı.

### POST `Join` — body `{ "matchId": 1, "side"?: 1 | 2, "slot"?: 0.. }`
`side` / `slot`: sahada dokunulan boş yer. O taraf doluysa normal kural (az kişili taraf) uygulanır; `slot` başkası
tarafından tutuluyorsa yer otomatik seçilir. Verilmezse eskisi gibi sunucu taraf seçer.
200 → `{ "success": true, "message": "Maça katıldın." | "Maç dolu, yedek listesine eklendin.", "status": 1 | 2 }`.
400: zaten katılıyor / maç geçmiş / reddedilmiş.

Maç detayında her katılımcının `phone` alanı, oyuncunun **Ayarlar → Telefonumu maçta kim görebilir** (`PlayerSettings.PhoneVisibility`:
0 kimse, 1 maç yöneticileri — varsayılan, 2 maçtaki herkes) izin verdiği görüntüleyene doludur; aksi halde `null`.

### POST `Leave` — body `{ "matchId": 1 }`
200 → `{ "success": true, "message": "..." }`. Kurucu da ayrılabilir.

### POST `Confirm` — body `{ "matchId": 1 }`
Going olmalı. Confirmed = true.

### POST `SetSide` — body `{ "matchId": 1, "playerId": 5, "side": 1 | 2 }`
canManage. Going oyuncunun tarafını değiştirir.

### POST `SubmitResult` — body
```json
{ "matchId": 1, "homeScore": 3, "awayScore": 2, "stats": [{ "playerId": 5, "goals": 2, "assists": 1 }] }
```
canManage + maç geçmiş. Skorlar 0–50, goals/assists 0–30. Going olmayan playerId'ler yok sayılır.

### POST `VoteMvp` — body `{ "matchId": 1, "votedPlayerId": 5 }`
### POST `RatePlayers` — body `{ "matchId": 1, "ratings": [{ "playerId": 5, "score": 4 }] }`
### POST `ToggleLike` — body `{ "matchId": 1 }` → `{ "liked": true, "likes": 5 }`

### Değişen mevcut uç noktalar
- `SaveMatch`: `CreatedByPlayerId = ben`; kurucu otomatik Going olarak eklenir (Side 1, Confirmed true).
- `DeleteMatch`: yetki = canManage kuralı.
- `UpdateMatch` — body `{ "matchId": 1, "matchName": "Çarşamba maçı", "description": "...", "userCount": 14 }`: yetki = canManage.
  Gönderilmeyen (null) alanlar değişmez; `description: ""` açıklamayı siler. Oynanmış / reddedilmiş maç düzenlenemez (`MATCH_EDIT_CLOSED`, admin hariç).
  `userCount` kadronun (Going + misafir) altına inemez (`MATCH_CAPACITY_BELOW_ROSTER`); azalınca taraf kapasitesini aşan son gelenler diğer tarafa kayar,
  artınca yedekler asile yükselir. Saha, saat, süre ve fiyat burada değişmez (harici sahada `UpdateExternalMatch`).
  Değişiklik kadrodaki ve yedekteki diğer oyunculara `MatchUpdated` (16) bildirimiyle gider. Admin ayrıca `matchDate` / `isActive` gönderebilir.
- `MatchLiked` (eski GET): `ToggleLike` ile aynı davranır ama sadece beğenir (geri uyumluluk).

## Oyuncu uç noktaları (`/api/Players/...`)
### GET `GetPlayerStats?playerId=`
```json
{ "playerId": 5, "matchesPlayed": 12, "goals": 9, "assists": 4, "mvpCount": 2,
  "avgRating": 4.2 | null, "ratingCount": 15, "userScore": 84,
  "recentMatches": [{ "matchId": 1, "matchName": "...", "matchDate": "...", "homeScore": 3, "awayScore": 2,
                      "side": 1, "goals": 1, "assists": 0, "wasMvp": false }] }  // son 5, geçmiş maçlar
```
matchesPlayed = geçmiş tarihli maçlarda Status=1 kayıt sayısı. mvpCount = en çok oyu aldığı (beraberlikte en düşük playerId) geçmiş maç sayısı.

### GET `GetLeaderboard?top=20`
```json
[{ "player": { /* PublicPlayerDto */ }, "matchesPlayed": 12, "goals": 9, "assists": 4, "mvpCount": 2,
   "avgRating": 4.2, "userScore": 84 }]
```
Admin hesaplar hariç; userScore, sonra matchesPlayed'e göre azalan.

---

# Ek: Misafir oyuncular (uygulamayı kullanmayan kişiler)

Maçı kuran kişi (canManage: kurucu ya da admin), kadrodaki boş yerlere uygulamada hesabı olmayan kişileri isimle ekleyebilir.

## Veri modeli — yeni tablo **MatchGuests**
| Alan | Tip | Not |
|---|---|---|
| GuestId | PK | |
| MatchId | FK Cascade | |
| Name | nvarchar(60) | 2–60 karakter, baştaki/sondaki boşluklar kırpılır |
| Note | nvarchar(200) null | Ör. "Kaleci, Ahmet'in arkadaşı" |
| Side | byte | 1 ev sahibi, 2 deplasman |
| AddedByPlayerId | FK Players NoAction | |
| CreatedAt | datetime | |

## Kurallar
- **Yetki:** sadece canManage. Maç geçmiş olmamalı, IsActive 3 (reddedilmiş) olmamalı. Aksi halde 400 ya da 403.
- **Kapasite:** misafirler kadroya sayılır.
  - Toplam: `Going + misafir < UserCount` olmalı.
  - Taraf: `o taraftaki Going + o taraftaki misafir < ceil(UserCount/2)` olmalı.
  - Dolu ise 400: `"Bu tarafta boş yer yok."`
- **Join:** Kapasite kontrolü misafirleri de hesaba katar. Misafir yüzünden dolu olan maça katılan Waitlist'e düşer. Taraf atamasında misafirler o tarafın sayısına eklenir.
- **Misafir silinince** boşalan yere Waitlist'teki ilk oyuncu yükselir ve misafirin tarafına atanır. Bildirim, mevcut yükseltme akışındaki gibi gider.
- **SetSide (canManage):** gerçek oyuncuyu dolu bir tarafa taşırken misafirler de sayılır.
- **Hariç tutulanlar:** Misafirler MVP oylamasına, oyuncu puanlamasına, istatistiklere ve sıralamaya dahil olmaz. SubmitResult `stats` içinde misafir yoktur.

## Uç noktalar (`/api/Match/...`)
| Metot | Yol | Gövde | Dönüş |
|---|---|---|---|
| POST | AddGuest | `{ matchId, side, name, note? }` | `{ success, message, guestId }` |
| POST | UpdateGuest | `{ guestId, name, note?, side }` | `{ success, message }`. Taraf değişiminde kapasite kontrolü yapılır |
| POST | RemoveGuest | `{ guestId }` | `{ success, message }` |

## Yanıtlara eklenenler
- `GetMatchDetail`: `"guests": [{ "guestId": 1, "name": "Ahmet", "note": "Kaleci", "side": 1, "addedBy": { /* PublicPlayerDto */ }, "createdAt": "..." }]`
- `GetMatchFeed` öğeleri: `"guestCount": 2`. `goingCount` artık **Going + misafir** toplamını döner, böylece doluluk halkası doğru gösterilir.
- `Owner/Reservations`, `Owner/Dashboard` ve `Owner/Calendar` öğelerindeki `goingCount`: Going + misafir.
- `GetMyCalendar` `goingCount`: Going + misafir.
