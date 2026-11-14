# Anlaşmalı Halı Sahalar (İşletme Rolü) — API Sözleşmesi
> **Hata gövdeleri:** bu dokümandaki tüm hata yanıtları artık standart gövdeyi kullanır: `{ success:false, code, message, errors?, traceId }`. Hata kodları, yerelleştirme ve parametreler için bkz. [platform-api.md](platform-api.md). Mesaj örnekleri `message` alanının Türkçe metnidir.

Uygulama artık sadece **anlaşmalı tesislerde** maç kurulmasına izin verir.

- Tesisler, admin tarafından üyelik verilen **İşletme** hesaplarınca yönetilir.
- Rezervasyon talebini işletme onaylar; admin onayı kalkar.
- Tüm uç noktalar `[Authorize]`. JSON camelCase. Hata gövdesi `{ success:false, message }` (400/403/404).
- Önceki sözleşmeler: `match-participation-api.md`, `social-api.md`, `tournaments-api.md`.

## Roller
`Players.Role` (byte) eklenir:
- 0 = Oyuncu
- 1 = Admin
- 2 = İşletme

`IsAdmin` geriye uyumluluk için kalır ve her zaman `Role == 1` ile senkron tutulur.

- JWT rol claim'i: `Player`, `Admin`, `Owner`.
- `PlayerDto` ve `PublicPlayerDto`'ya `role` (0/1/2) alanı eklenir.
- İşletme hesapları oyuncu listelerinde görünmez: GetPlayerList, Discover, Suggestions, Leaderboard, takım ve davet seçicileri.
- İşletme hesabı maç kuramaz, maça katılamaz, takım kuramaz ve takip edilemez (400/403).

**Üyelik:** Players'a `MembershipUntil` (datetime null) ve `MembershipSuspended` (bit) eklenir; sadece işletmeler için anlamlıdır.

**Aktif işletme** = Role 2, askıda değil, `MembershipUntil` boş ya da gelecekte.

Askıda ya da süresi dolmuş işletme:
- Giriş yapabilir ama paneli salt okunur olur: yazma uç noktaları 403 `"Üyeliğiniz aktif değil."` döner.
- Tesisleri oyunculara görünmez.

## Veri modeli

### Stadiums (mevcut tablo "Tesis" olarak genişletilir)
Mevcut alanlar korunur: StadiumName, City, District, Location `"lat, lon"`, Address, OpeningTime, ClosingTime, Description.

Eklenen alanlar:

| Alan | Tip | Not |
|---|---|---|
| OwnerPlayerId | int? FK Players (NoAction) | |
| Phone | nvarchar(30) null | |
| IsListed | bit | Admin "yayında" anahtarı, varsayılan 1 |
| CleatRental | bit | Krampon kiralama var mı |
| CleatRentalPrice | decimal(10,2) null | Krampon kiralama ücreti |
| HasCamera | bit | Maç kaydı var mı |
| CameraPrice | decimal(10,2) null | Maç kaydı ücreti |
| HasShower | bit | |
| HasLockerRoom | bit | |
| HasParking | bit | |
| HasCafe | bit | |
| HasShuttle | bit | |
| ProvidesBibs | bit | Yelek |
| ProvidesBall | bit | Top |
| CancellationPolicy | nvarchar(500) null | |
| OsmId | bigint null | OpenStreetMap kaynağı |
| CreatedAt | datetime | |

**Görünürlük:** Bir tesis oyunculara ancak şu koşulların hepsi sağlanırsa listelenir:
- `IsListed`
- Aktif bir işletmesi var
- En az 1 fotoğrafı ve en az 1 aktif sahası var
- Location ve Address dolu

### Diğer yeni tablolar

**StadiumPhotos**

| Alan | Tip | Not |
|---|---|---|
| PhotoId | PK | |
| StadiumId | FK Cascade | |
| Url | nvarchar(300) | `/uploads/venues/<guid>.jpg` |
| IsCover | bit | |
| SortOrder | int | |

**Pitches** (tesisteki sahalar)

| Alan | Tip | Not |
|---|---|---|
| PitchId | PK | |
| StadiumId | FK Cascade | |
| Name | nvarchar(60) | |
| Surface | byte | 1 suni çim, 2 doğal çim, 3 parke, 4 diğer |
| IsIndoor | bit | |
| PlayersPerSide | int | 5–11 |
| BasePrice | decimal(10,2) | Saatlik |
| IsActive | bit | |

**OpeningHours**

| Alan | Tip | Not |
|---|---|---|
| OpeningHourId | PK | |
| StadiumId | FK Cascade | |
| DayOfWeek | byte | 0 Pazartesi … 6 Pazar |
| IsClosed | bit | |
| OpenTime | time | |
| CloseTime | time | `00:00` = gece yarısı |

Unique (StadiumId, DayOfWeek). Kayıt yoksa Stadiums.OpeningTime/ClosingTime kullanılır.

**PriceRules**

| Alan | Tip | Not |
|---|---|---|
| PriceRuleId | PK | |
| StadiumId | FK Cascade | |
| PitchId | null FK | null = tüm sahalar |
| DaysMask | int | bit 0 = Pazartesi … bit 6 = Pazar |
| FromHour | int 0–23 | |
| ToHour | int 1–24, exclusive | |
| PricePerHour | decimal | |

Saatin fiyatı:
1. O saati kapsayan kural, sahaya özel kural genele göre önceliklidir.
2. Kural yoksa Pitch.BasePrice.

**Campaigns**

| Alan | Tip | Not |
|---|---|---|
| CampaignId | PK | |
| StadiumId | FK Cascade | |
| PitchId | null | |
| Title | nvarchar(80) | |
| Description | nvarchar(300) null | |
| StartDate | date | |
| EndDate | date | |
| DaysMask | int | |
| FromHour | int | |
| ToHour | int | |
| DiscountPercent | int null | 1–90 |
| FixedPrice | decimal null | |
| IsActive | bit | |

DiscountPercent ve FixedPrice'tan tam olarak biri dolu olmalı. Bir saate birden fazla kampanya uyuyorsa **en ucuz sonuç** uygulanır.

**SlotBlocks** (işletmenin elle kapattığı saatler)

| Alan | Tip | Not |
|---|---|---|
| SlotBlockId | PK | |
| PitchId | FK Cascade | |
| StartAt | datetime | |
| EndAt | datetime | |
| Reason | nvarchar(100) | |

**VenueReviews**

| Alan | Tip | Not |
|---|---|---|
| ReviewId | PK | |
| StadiumId | FK Cascade | |
| PlayerId | FK NoAction | |
| MatchId | FK NoAction | |
| Rating | byte 1–5 | |
| Comment | nvarchar(500) null | |
| OwnerReply | nvarchar(500) null | |
| RepliedAt | null | |
| CreatedAt | | |

Unique (MatchId, PlayerId).

### Matchs'a eklenenler

| Alan | Tip | Not |
|---|---|---|
| PitchId | int? FK NoAction | |
| DurationMinutes | int | Varsayılan 60; 60, 90 veya 120 |
| PitchPrice | decimal null | Rezervasyon anındaki saha ücreti |
| ExtrasPrice | decimal null | |
| TotalPrice | decimal null | |
| CleatCount | int | Varsayılan 0 |
| CameraRequested | bit | |
| HomeTeamId | int? FK NoAction | |
| AwayTeamId | int? FK NoAction | |
| LookingForOpponent | bit | |
| RejectReason | nvarchar(200) null | |

`Match.IsActive` anlamını korur: 1 bekliyor, 2 onaylı, 3 reddedildi. Bundan sonra onaylayan/reddeden işletmedir.

**MatchInvites'a eklenen:** `Side` byte null. Takım daveti kabul edilince oyuncu bu tarafa yerleşir; taraf doluysa normal Join kuralı uygulanır.

## Fiyat hesabı
- Maç `[start, start + duration)` aralığını saat dilimlerine böler. Örneğin 90 dakika = 1 tam saat + yarım saat. Her dilime o saatin ücreti oranlı uygulanır.
- `pitchTotal` = dilimlerin toplamı, kampanya uygulanmış hâliyle.
- `extrasTotal` = `cleatCount * CleatRentalPrice` + (kamera istendiyse `CameraPrice`).
- `total` = pitchTotal + extrasTotal.
- `cleatTotal` = `cleatCount * CleatRentalPrice`. Krampon ücretini yalnızca kiralayan öder; kişi başına bölünmez.
- `perPlayer` = ceil((total − cleatTotal) / userCount). Krampon kiralayan buna ek olarak `cleatPrice` (bir çift) öder.
- Tutarlar ₺ olarak decimal(10,2).

## Müsaitlik kuralları
Bir dilim **dolu** sayılır, eğer aynı sahada şunlardan biriyle çakışıyorsa:
- IsActive 1 veya 2 olan bir maç
- Bir SlotBlock

Ayrıca:
- Açılış saatleri dışında kalan dilim **kapalı**dır. Gece yarısını aşan kapanış desteklenir: CloseTime < OpenTime ise ertesi güne taşar.
- Geçmiş saatler **past**tır.

`SaveMatch` sunucuda tekrar kontrol eder ve çakışma varsa 400 `"Bu saat dolu."` döner. Eşzamanlılık için ilgili Pitch satırında UPDLOCK kullanılır. Eski "aynı saatte başka maç var" kontrolü kaldırılır; artık sadece aynı saha önemlidir.

## Oyuncu uç noktaları — `/api/Venues/...`

### GET `List?city=&q=&date=`
Görünür tesisler.
```json
[{ "stadiumId": 1, "name": "...", "city": "İstanbul", "district": "Kadıköy", "address": "...", "location": "41.0, 29.0",
   "coverPhotoUrl": "/uploads/venues/x.jpg", "pitchCount": 2,
   "amenities": { "cleatRental": true, "camera": true, "shower": true, "lockerRoom": true, "parking": false, "cafe": true, "shuttle": false, "bibs": true, "ball": true },
   "minPrice": 1200, "hasActiveCampaign": true, "rating": 4.6, "reviewCount": 18,
   "freeSlotsToday": 5 }]   // date verilirse o gün için boş saat sayısı
```
`city` ve `district` metin olarak döner. Stadiums.City/District int id ise isimleri çözülemiyorsa id'nin string'i döner (seed verisinde metin kullanılacak; bkz. seed).

### GET `Detail?stadiumId=`
```json
{ /* List alanları */
  "description": "...", "phone": "...", "cancellationPolicy": "...",
  "photos": [{ "photoId": 1, "url": "...", "isCover": true }],
  "pitches": [{ "pitchId": 1, "name": "Saha 1", "surface": 1, "isIndoor": true, "playersPerSide": 7, "basePrice": 1500 }],
  "openingHours": [{ "dayOfWeek": 0, "isClosed": false, "openTime": "09:00", "closeTime": "02:00" }],  // 7 gün
  "prices": { "cleatRentalPrice": 100, "cameraPrice": 300 },
  "campaigns": [{ "campaignId": 1, "title": "Salı Öğlen İndirimi", "description": "...", "startDate": "...", "endDate": "...",
                  "daysMask": 2, "fromHour": 12, "toHour": 16, "discountPercent": 25, "fixedPrice": null, "pitchId": null }], // aktif ve bitmemiş
  "owner": { "playerId": 12, "name": "Yeşil Vadi Spor Tesisleri" },   // mesaj atmak için
  "reviews": [{ "reviewId": 1, "rating": 5, "comment": "...", "createdAt": "...", "player": { /* PublicPlayerDto */ },
                "ownerReply": "...", "repliedAt": "..." }],   // son 20
  "ratingBreakdown": { "1": 0, "2": 1, "3": 2, "4": 5, "5": 10 } }
```

### GET `Availability?stadiumId=&date=YYYY-MM-DD`
O günün dilimleri, saatlik ve tüm aktif sahalar için.
```json
{ "date": "2026-11-20", "isClosed": false,
  "pitches": [{ "pitchId": 1, "name": "Saha 1",
     "slots": [{ "start": "2026-11-20T18:00:00", "end": "2026-11-20T19:00:00",
                 "status": "free" | "booked" | "pending" | "blocked" | "past",
                 "price": 1350, "originalPrice": 1800, "campaignTitle": "..." | null }] }] }
```
"pending" = onay bekleyen bir maç var; oyuncuya dolu gibi gösterilir.

### POST `Quote` — `{ pitchId, start, durationMinutes, userCount, cleatCount, cameraRequested }`
```json
{ "available": true, "reason": null,
  "lines": [{ "label": "18:00–19:00", "price": 1350, "originalPrice": 1800, "campaignTitle": "..." }],
  "pitchTotal": 2025, "extrasTotal": 300, "total": 2325, "cleatTotal": 100, "cleatPrice": 50, "perPlayer": 223 }
```
Müsait değilse `available:false` ve sebep döner (yine 200).

### POST `Review` — `{ matchId, rating, comment? }`
- Maç geçmiş, maç bu tesiste (Pitch → Stadium) ve ben o maçta Going olmalıyım.
- Upsert yapılır.
- İşletmeye bildirim gider.

## Maç kurma (değişen `POST /api/Match/SaveMatch`)
Yeni gövde:
```json
{ "matchName": "...", "description": "...", "userCount": 14,
  "pitchId": 3, "matchDate": "2026-11-20T18:00:00", "durationMinutes": 60,
  "cleatCount": 2, "cameraRequested": true,
  "homeTeamId": 5 | null, "awayTeamId": 9 | null, "lookingForOpponent": false }
```

**Kurallar:**
- `pitchId` zorunlu. Tesis görünür olmalı. Dakika 0 ya da 30 olmalı. Açılış saatleri içinde olmalı ve dolu olmamalı.
- `userCount` en az 2 × 4, en çok `PlayersPerSide * 2 + 4`.
- `StadiumId` pitch'ten set edilir. Fiyatlar Quote mantığıyla hesaplanıp kaydedilir.
- `IsActive = 1` olur.
- Kurucu Going olur (önceki sözleşmedeki gibi).
- `homeTeamId` verildiyse kurucu o takımın üyesi olmalı. Kurucu Side 1 olur; takımın diğer üyelerine `MatchInvite` (Side 1) gider.
- `awayTeamId` verildiyse, ev sahibi takımla aynı olamaz. Takımın üyelerine MatchInvite (Side 2) gider. Bu uç nokta için kurucunun o takımın üyesi olması gerekmez.
- `lookingForOpponent` sadece `awayTeamId` null iken true olabilir.
- İşletmeye bildirim gider: yeni tür `ReservationRequest = 13`, alıcı tercihi NotifyMatchUpdates.

Eski gövde (`stadiumId`, `isActive`) artık 400 döner: `"Lütfen bir saha ve saat seçin."`

Başarılı yanıt: `{ success: true, message, matchId, status }`.

`GetMatchFeed` ve `GetMatchDetail` yanıtlarına eklenenler:
```json
"venue": { "stadiumId": 1, "name": "...", "pitchId": 3, "pitchName": "Saha 1", "isIndoor": true, "surface": 1, "coverPhotoUrl": "..." } | null,
"durationMinutes": 60, "totalPrice": 2325, "perPlayer": 223, "cleatPrice": 50, "cleatCount": 2, "cameraRequested": true,
"homeTeam": { "teamId": 5, "teamName": "..." } | null, "awayTeam": { ... } | null, "lookingForOpponent": false,
"rejectReason": null
```
Sadece GetMatchDetail'e:
- `"pitchPrice": 1800, "extrasPrice": 300`: kayıtlı ücretler
- `venue.cancellationPolicy`: tesisin iptal politikası
- `"ownerPlayerId": 12`: maç merkezinden işletmeye mesaj atmak için
- `"canReview": true`
- `"myReview": { "rating": 5, "comment": "..." } | null`

**Rakip takım olarak katılma:** `POST /api/Match/ClaimOpponent { matchId, teamId }`
- `lookingForOpponent` true olmalı ve ben o takımın üyesi olmalıyım.
- `AwayTeamId` set edilir, LookingForOpponent false olur.
- Ben Going (Side 2) olurum; takımın diğer üyelerine davet (Side 2) gider.

**MatchStatusUpdate (admin):** Kalır ama admin artık sadece "geçersiz kılma" için kullanır. Asıl yol işletme uç noktasıdır.

## Mesaj izni eklentisi (social-api.md'deki CanMessage)
İki kişi arasında **rezervasyon ilişkisi** varsa mesaj her zaman izinlidir. Rezervasyon ilişkisi şu demektir:
- Biri işletme,
- diğeri o işletmenin tesislerinden birinde, geçmiş 30 gün ya da gelecekteki, IsActive 1 veya 2 olan bir maçın **kurucusu** ya da Going oyuncusu.

İşletmeler `MessageContacts` listesine sadece yazıştıkları kişilerle gelir. Takip mantığı işletmelere uygulanmaz.

## İşletme uç noktaları — `/api/Owner/...` (`[Authorize(Roles="Owner")]`)
Her istek, `stadiumId` ya da dolaylı tesis sahibi `OwnerPlayerId == ben` kontrolünden geçer; aksi halde 403. Yazma uç noktaları üyelik aktif değilse 403.

| Metot | Yol | Gövde / Query | Dönüş |
|---|---|---|---|
| GET | MyVenues | | `[{ stadiumId, name, coverPhotoUrl, pitchCount, isVisible, missing: ["photo","pitch",...] }]` + üyelik bilgisi: tüm dönüş `{ membershipUntil, membershipActive, venues:[...] }` |
| GET | Dashboard | `?stadiumId=` | `{ todayCount, weekCount, pendingCount, occupancyPercent (bu hafta açık saatlerin dolu oranı), weekRevenue (onaylı maçların TotalPrice toplamı), rating, reviewCount, upcoming: [ReservationDto, ilk 5], pending: [ReservationDto] }` |
| GET | Reservations | `?stadiumId=&status=&from=&to=` | `ReservationDto[]` |
| POST | RespondReservation | `{ matchId, approve, reason? }` | Sadece IsActive 1 iken. Onay → 2, red → 3 + RejectReason. Kurucu ve Going oyunculara MatchApproval/MatchRejection bildirimi (mevcut tipler 2/3) |
| GET | Calendar | `?stadiumId=&from=&to=` | `{ pitches:[{pitchId,name}], openingHours:[...7], items:[{ kind:"match", matchId, pitchId, start, end, status(1/2/3), title, creator, goingCount, capacity, totalPrice } \| { kind:"block", slotBlockId, pitchId, start, end, reason }] }` |
| POST | Venue/Update | `{ stadiumId, name, description, phone, address, city, district, location, cancellationPolicy, cleatRental, cleatRentalPrice, hasCamera, cameraPrice, hasShower, hasLockerRoom, hasParking, hasCafe, hasShuttle, providesBibs, providesBall }` | Location "lat, lon" doğrulanır. Address boş olamaz |
| POST | Photos/Upload | multipart `stadiumId`, `file` (jpg/png ≤5MB, magic byte kontrolü) | `{ photoId, url }`. İlk foto otomatik kapak olur |
| POST | Photos/Delete | `{ photoId }` | Son fotoğraf silinemez (400) |
| POST | Photos/SetCover | `{ photoId }` | |
| GET | Pitches | `?stadiumId=` | Pasifler dahil |
| POST | Pitches/Save | `{ pitchId?, stadiumId, name, surface, isIndoor, playersPerSide, basePrice, isActive }` | Oluştur ya da güncelle |
| POST | Pitches/Delete | `{ pitchId }` | Gelecekte onaylı/bekleyen maçı varsa 400; yerine pasife alınması önerilir |
| GET / POST | OpeningHours | `?stadiumId=` / `{ stadiumId, days:[7 kayıt] }` | |
| GET / POST | PriceRules, PriceRules/Save, PriceRules/Delete | | |
| GET / POST | Campaigns, Campaigns/Save, Campaigns/Delete | | |
| GET / POST | Blocks `?stadiumId=&from=&to=`, Blocks/Save `{ pitchId, startAt, endAt, reason }`, Blocks/Delete | | Blok, mevcut bekleyen/onaylı maçla çakışırsa 400 |
| GET | Reviews | `?stadiumId=` | |
| POST | Reviews/Reply | `{ reviewId, reply }` | |

**ReservationDto:**
```json
{ "matchId": 1, "matchName": "...", "start": "...", "end": "...", "durationMinutes": 60,
  "pitch": { "pitchId": 3, "name": "Saha 1" }, "status": 1,
  "creator": { /* PublicPlayerDto + phoneNumber */ },
  "goingCount": 9, "capacity": 14,
  "homeTeam": { "teamId": 5, "teamName": "..." } | null, "awayTeam": { ... } | null,
  "cleatCount": 2, "cameraRequested": true,
  "pitchPrice": 1800, "extrasPrice": 300, "totalPrice": 2100,
  "createdAt": "...", "rejectReason": null }
```
İşletme, kurucunun telefonunu görebilir (iletişim için).

## Admin uç noktaları — `/api/Admin/...`
| Metot | Yol | Gövde | Not |
|---|---|---|---|
| GET | Owners | | `[{ owner: { playerId, name, userName, email, phoneNumber }, membershipUntil, membershipSuspended, membershipActive, venues:[{ stadiumId, name, isVisible }], createdAt }]` |
| POST | CreateOwner | multipart. `model` JSON: `{ owner: { name, contactName, userName, email, phoneNumber, password, membershipUntil }, venue: { name, city, district, address, location, phone, description } }`; `photos`: 1–10 dosya | **Fotoğraf (en az 1), konum ve adres zorunlu**; eksikse 400 alan bazlı mesaj. Tek transaction: işletme hesabı (PlayerName = işletme adı, PlayerSurname = contactName), tesis (OwnerPlayerId), fotoğraflar (ilki kapak), varsayılan "Saha 1" (7+7, suni çim, kapalı, BasePrice 1500) ve 7 günlük 09:00–00:00 açılış saati |
| POST | UpdateMembership | `{ ownerId, membershipUntil, suspended }` | |
| POST | AssignVenue | `{ stadiumId, ownerId }` | |
| POST | SetListed | `{ stadiumId, isListed }` | |
| GET | OsmCandidates | `?city=` veya `?south=&west=&north=&east=` | Sunucu tarafında Overpass API sorgusu (`https://overpass-api.de/api/interpreter`, timeout 25 sn, User-Agent `MatcBuddy/1.0`): `leisure=pitch` + `sport~soccer` node/way (way için center). Dönüş `[{ osmId, name, lat, lon, address, phone, website, openingHours, alreadyExists }]`. alreadyExists: aynı OsmId ya da 150 m içinde bir tesis var. `city` verildiyse şehir sınırı Nominatim ile bulunur. Hata/timeout → 502 `{ message }` |
| POST | ImportCandidate | `{ osmId, name, lat, lon, address, city, district, phone }` | IsListed = 1 ama işletmesi olmadığı için görünmez; OsmId set edilir. Dönüş `{ stadiumId }` |

## İşletme üyelik başvurusu — `/api/BusinessApplications`

İşletmeler sayfasındaki "Başvur" formu (`/isletme-basvuru`). İşletme hesabı kendiliğinden açılmaz: başvuru kaydedilir,
yöneticilere e-posta gider; yönetici başvuranı arar ve hesabı `POST /api/Admin/...` işletme sihirbazıyla açar.

| Uç nokta | Yetki | Açıklama |
|---|---|---|
| `POST /api/BusinessApplications` | herkese açık (oran sınırı `auth`) | `{ businessName*, contactName*, phone*, email?, city?, district?, pitchCount? (1–50), note? (≤1000), consent* }` → `{ success, message }`. Telefon `05xxxxxxxxx` biçimine çevrilir. Aynı numaradan 24 saat içinde ikinci başvuru kaydedilmez (yine "alındı" döner). |
| `GET /api/BusinessApplications?status=` | Admin | Yeni (0) olanlar önce, sonra en yeni; en fazla 300. |
| `GET /api/BusinessApplications/NewCount` | Admin | Menü rozeti için yeni başvuru sayısı. |
| `POST /api/BusinessApplications/Update` | Admin | `{ businessApplicationId, status (0 yeni, 1 arandı, 2 hesap açıldı, 3 olumsuz), adminNote? }` |

Hata kodları: `BIZAPP_CONSENT_REQUIRED`, `BIZAPP_NOT_FOUND`, alan doğrulamaları `VALIDATION_*`.

## Bildirim türleri (NotificationType sonuna)
- `ReservationRequest = 13`
- `VenueReview = 14`

İkisi de NotifyMatchUpdates tercihine bağlıdır.

## Veri sıfırlama ve demo verisi (tek seferlik script)
Silinmeden önce `BACKUP DATABASE` ile tam yedek alınır.

Sonra şunlar silinir:
- Tüm maçlar ve bağlı satırları: participants, comments, likes, votes, ratings, invites, activities (MatchId dolu olanlar), maçlara bağlı notification ve player notification'lar, MatchTeam
- Turnuva fikstürleri ve turnuvalar
- Tüm stadyumlar

Oyuncular, takımlar, takipler, mesajlar ve gruplar **korunur**.

Ardından seed eklenir:
- **5 demo işletme ve tesis:** İstanbul 3 (Kadıköy, Beşiktaş, Ataşehir), Ankara 1 (Çankaya), İzmir 1 (Bornova).
  - Gerçekçi isimler, adresler ve koordinatlar.
  - Her birinde 1–3 saha, açılış saatleri, akşam saatleri için pahalı fiyat kuralı, 1 aktif kampanya ve değişen olanaklar.
  - Fotoğraflar: `MatcBuddyReact` git geçmişindeki stadyum görselleri (`stadyum*.jpg`, `foto*.jpg`) `wwwroot/uploads/venues/` altına kopyalanır.
  - Kullanıcı adları `isletme_<kısa ad>`, şifre `Isletme!2026`. Bunlar sadece yerel demo verisidir; `docs/demo-accounts.md` dosyasına yazılır.
- **Demo maçlar:**
  - Mevcut oyunculardan rastgele kurucu ve katılımcılarla 12 gelecek maç (bir kısmı bekliyor, bir kısmı onaylı, 2 tanesi takım maçı, 1 tanesi "rakip aranıyor")
  - 6 geçmiş maç: skor, MVP oyları, puanlar ve tesis yorumları ile

## Sonradan eklenen küçük alanlar
- `GET /api/Stadium/GetStadiumList` öğelerine `isListed` ve `ownerPlayerId` alanları eklenir.
- `GET /api/Admin/Owners` yanıtındaki `owner` nesnesine `contactName` alanı eklenir.
- `POST /api/Match/MatchStatusUpdate` (admin müdahalesi) opsiyonel `reason` alır; reddedilen maçta RejectReason'a yazılır.
- Matchs tablosuna `CreatedAt` alanı eklenir. Bu alan ReservationDto'da ve GetMatchFeed öğelerinde `createdAt` olarak döner.
- `GET /api/Owner/Venue?stadiumId=`: işletmenin kendi tesisi, görünürlükten bağımsız. Dönüş Venues/Detail şeklinde; pasif sahalar dahil, ayrıca `isListed` ve `missing` alanları var.
- Saat alanları istekte "HH:mm" ya da "HH:mm:ss" kabul eder; yanıtta "HH:mm" döner.
- İşletme liste uç noktaları (Pitches, OpeningHours, PriceRules, Campaigns, Reviews) düz dizi döner. Reviews öğeleri `match: { matchId, matchName, matchDate }` alanını içerir.

---

# Ek paket: Harici rezervasyon, gelir-gider takibi, takım kadrosu düzeltmesi

## 1. Harici rezervasyon (uygulama dışından gelen maçlar)
İşletme, telefonla ya da elden aldığı rezervasyonları takvime girebilir. Bunun için mevcut `SlotBlocks` tablosu genişletilir:

| Yeni alan | Tip | Not |
|---|---|---|
| Kind | byte, varsayılan 0 | 0 = Kapalı/bakım (eski davranış), 1 = Harici rezervasyon |
| CustomerName | nvarchar(80) null | Kind 1 için zorunlu |
| CustomerPhone | nvarchar(30) null | |
| Price | decimal(10,2) null | Kind 1 için zorunlu, 0 veya üstü |
| IsPaid | bit, varsayılan 0 | Ödeme alındı mı |
| Note | nvarchar(200) null | |

- `Reason` kind 0 için zorunlu kalır; kind 1'de opsiyoneldir (başlık gibi kullanılır, ör. "Şirket maçı").
- Çakışma kuralları aynıdır. Harici rezervasyon da slotu doldurur; oyunculara "booked" (dolu) olarak görünür, "blocked" olarak değil.
- `POST /api/Owner/Blocks/Save` gövdesi: `{ slotBlockId?, pitchId, startAt, endAt, kind, reason?, customerName?, customerPhone?, price?, isPaid?, note? }`. `slotBlockId` verilirse kayıt güncellenir (çakışma kontrolünde kendisi hariç tutulur).
- Yeni: `POST /api/Owner/Blocks/SetPaid { slotBlockId, isPaid }`.
- `GET /api/Owner/Blocks` ve `GET /api/Owner/Calendar` blok öğeleri yeni alanların tamamını döner: `kind, customerName, customerPhone, price, isPaid, note`.
- Yeni: `POST /api/Owner/Quote { pitchId, start, durationMinutes }` → `{ pitchTotal, lines }`. İşletmenin kendi fiyat kurallarıyla saha ücretini hesaplar; tesis görünür olmasa da çalışır. Harici rezervasyon formunda fiyatı önceden doldurmak için kullanılır.

## 2. Giderler
Yeni tablo **VenueExpenses**:

| Alan | Tip | Not |
|---|---|---|
| ExpenseId | PK | |
| StadiumId | FK Cascade | |
| Date | date | |
| Category | nvarchar(40) | |
| Amount | decimal(12,2) | > 0 |
| Description | nvarchar(200) null | |
| CreatedAt | datetime | |

Kategoriler (sabit liste, frontend de kullanır): `Personel`, `Kira`, `Elektrik`, `Su`, `Doğalgaz`, `Bakım-onarım`, `Malzeme`, `Vergi`, `Reklam`, `Diğer`.

| Metot | Yol | Not |
|---|---|---|
| GET | `/api/Owner/Expenses?stadiumId=&from=&to=` | Düz dizi, tarihe göre azalan |
| POST | `/api/Owner/Expenses/Save` | `{ expenseId?, stadiumId, date, category, amount, description? }` |
| POST | `/api/Owner/Expenses/Delete` | `{ expenseId }` |

Yazma kuralları diğer işletme uç noktalarıyla aynıdır: tesis sahipliği kontrolü, üyelik aktif değilse 403.

## 3. Gelir hesabı (otomatik)
- **Uygulama maçı geliri:** IsActive = 2 (onaylı), PitchId dolu ve **bitmiş** (start + DurationMinutes ≤ şimdi) maçların `TotalPrice` toplamı.
- **Harici rezervasyon geliri:** Kind = 1, bitmiş ve `IsPaid = true` olan blokların `Price` toplamı.
- **Tahsil edilmemiş:** Kind = 1, bitmiş ve `IsPaid = false` olan blokların toplamı.
- **Beklenen gelir:** Henüz oynanmamış onaylı maçlar ile harici rezervasyonların toplamı.
- Gelir, maçın başlangıç tarihinin ayına yazılır.

## 4. Finans özeti — `GET /api/Owner/Finance?stadiumId=&year=&month=`
```json
{ "year": 2026, "month": 11,
  "income": { "matches": 18400, "manual": 6200, "total": 24600 },
  "receivable": 1500,
  "expectedIncome": 9800,
  "expenses": 11200,
  "net": 13400,
  "marginPercent": 54.5,
  "previous": { "income": 21000, "expenses": 10500, "net": 10500 },   // önceki ay, kıyas için
  "counts": { "matches": 14, "manualBookings": 6, "hoursSold": 22.5 },
  "daily": [{ "date": "2026-11-01", "income": 1200, "expense": 0 }],          // ayın her günü
  "incomeByPitch": [{ "pitchId": 1, "name": "Saha 1", "income": 9000, "hours": 8 }],
  "incomeBySource": [{ "source": "matches", "amount": 18400 }, { "source": "manual", "amount": 6200 }],
  "expenseByCategory": [{ "category": "Personel", "amount": 6000 }],
  "monthly": [{ "year": 2025, "month": 12, "income": 0, "expenses": 0, "net": 0 }],  // son 12 ay, eskiden yeniye, seçili ay dahil
  "transactions": [{ "date": "2026-11-05T20:00:00", "type": "income" | "expense",
                     "source": "match" | "manual" | "expense",
                     "title": "Perşembe Maçı" | "Şirket maçı – Ahmet Y." | "Elektrik faturası",
                     "category": null | "Elektrik", "pitchName": "Saha 1" | null,
                     "amount": 2100, "isPaid": true, "refId": 123 }] }    // seçili ayın tüm hareketleri, tarihe göre azalan
```
`hoursSold`: bitmiş onaylı maçlar ve bitmiş harici rezervasyonların toplam süresi (saat).

## 5. Takım kadrosu düzeltmesi (SaveMatch ve ClaimOpponent)
Eski davranış: takım üyelerine sadece davet gidiyordu ve kadroda görünmüyorlardı. Yeni davranış:
- `homeTeamId` verildiyse takımın **tüm üyeleri** (kurucu dahil) doğrudan maça eklenir: Status = Going, Side = 1, Confirmed = false; kurucu Confirmed = true.
- `awayTeamId` verildiyse o takımın üyeleri de doğrudan eklenir: Going, Side = 2.
- `ClaimOpponent` aynı şekilde talep eden takımın tüm üyelerini Side 2 olarak ekler.
- Kapasite aşılırsa fazlası Waitlist olur (JoinedAt sırasıyla).
- İşletme hesapları ve silinmiş hesaplar atlanır. Kurucuyu engelleyen ya da kurucunun engellediği oyuncular atlanmaz (takım üyeliği önceliklidir).
- Eklenen her üyeye (kurucu hariç) bildirim gider: `MatchJoin` tipi, metin "X seni takımın 'Y' maçının kadrosuna ekledi. Gelemeyeceksen maçtan ayrıl." ve NotifyMatchUpdates tercihine uyulur.
- Takım üyeleri için artık MatchInvite oluşturulmaz.
- JoinedMatch aktivitesi sadece kurucu için yazılır; takım arkadaşları için yazılmaz (akış kalabalıklaşmasın).

### Netleştirmeler
- `GET /api/Owner/Calendar` öğelerinde `kind` alanı "match" ya da "block" metni olarak kalır. Blok öğelerinde blok türü ayrı bir `blockKind` alanında döner (0 = kapalı, 1 = harici rezervasyon); `customerName`, `customerPhone`, `price`, `isPaid` ve `note` de bu öğelerde yer alır.
- `GET /api/Owner/Blocks` listesinde blok türü sayısal `kind` alanında döner; aynı değer `blockKind` alanında da tekrarlanabilir.
- Finans `transactions[].refId` alanının anlamı kaynağa göre değişir:
  - "match" → MatchId
  - "manual" → SlotBlockId
  - "expense" → ExpenseId
- Tarih aralığı parametrelerinde `from` ve `to` iki uçta da dahildir (tüm gün).
