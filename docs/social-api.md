# Sosyal Paket — API Sözleşmesi

Takip, mesaj izni, gizlilik, engelle/şikayet, davetler, akış, takvim, ayarlar ve hesap silme.
Tüm uç noktalar `[Authorize]`, "ben" = JWT'deki oyuncu. JSON camelCase.
Hata gövdesi: `{ "success": false, "message": "..." }` (400/403/404).
Önceki sözleşme: `docs/match-participation-api.md` (PublicPlayerDto, maç feed/detail yapıları).

## Kavramlar
- **Takip** tek yönlüdür. A, B'yi takip eder; B isterse A'yı geri takip eder.
- **Arkadaş = karşılıklı takip** (`isMutual`).
- **Birlikte oynamak:** iki oyuncunun aynı maçta `MatchParticipant.Status = 1` (Going) kaydı vardır (maç tarihi fark etmez).
- **Engel:** A, B'yi engellerse aralarındaki tüm takipler (iki yönlü) silinir. İki taraf birbirini listelerde, aramada, akışta ve önerilerde görmez. B, A'nın profilini açarsa 404 alır. Mesaj, takip ve davet gönderilemez (403). Kontroller her zaman iki yönlüdür.
- **Silinmiş hesap** (`Player.IsDeleted = true`) hiçbir listede görünmez ve giriş yapamaz.

## Veri modeli (yeni)
| Tablo | Alanlar |
|---|---|
| Follows | FollowId PK, FollowerId FK, FolloweeId FK, CreatedAt. Unique (FollowerId, FolloweeId) |
| Blocks | BlockId PK, BlockerId FK, BlockedId FK, CreatedAt. Unique (BlockerId, BlockedId) |
| Reports | ReportId PK, ReporterId FK, ReportedPlayerId FK, Reason nvarchar(50), Details nvarchar(500) null, Status byte (0 yeni, 1 incelendi), CreatedAt |
| PlayerSettings | PlayerId PK+FK (1:1), MessagePermission byte (0 herkes, 1 sadece arkadaşlar, **2 arkadaşlar + birlikte oynadıklarım = varsayılan**), ProfileVisibility byte (**0 herkes = varsayılan**, 1 takipçilerim, 2 sadece ben), NotifyFollow, NotifyMatchInvite, NotifyTeamInvite, NotifyMatchReminder, NotifyMatchResult, NotifyMatchUpdates (bool, varsayılan true) |
| MatchInvites | InviteId PK, MatchId FK (Cascade), InviterId FK, InviteeId FK, Status byte (0 bekliyor, 1 kabul, 2 red), CreatedAt, RespondedAt null. Unique (MatchId, InviteeId) |
| TeamInvites | InviteId PK, TeamId FK (Cascade), InviterId FK, InviteeId FK, Status, CreatedAt, RespondedAt. Unique (TeamId, InviteeId) |
| Activities | ActivityId PK, ActorPlayerId FK, Type byte, MatchId null FK (Cascade), TeamId null, TargetPlayerId null, Value int null, CreatedAt |

Players tablosuna eklenir: `IsDeleted bit default 0`, `DeletedAt datetime null`.
Settings kaydı yoksa varsayılanlar kullanılır (lazy oluşturma).
Player FK'leri NoAction.

**Activity.Type:**
- 1 MatchCreated (SaveMatch)
- 2 JoinedMatch (Going olarak katıldı ya da yedekten asile geçti)
- 3 Scored (SubmitResult; goals > 0 olan her oyuncu için, Value = goals; sonuç düzeltilirse o maçın Scored kayıtları yeniden yazılır)
- 4 MvpWon (MVP oyu her değiştiğinde o maçın MvpWon kaydı güncel lidere göre upsert edilir; oy yoksa silinir)
- 5 TeamCreated
- 6 Followed (TargetPlayerId)
- 7 TournamentWon (turnuva paketi kullanacak; şimdilik sadece enum)

**NotificationType (sona eklenir):** NewFollower = 9, MatchInvite = 10, TeamInvite = 11.
Bildirimler alıcının PlayerSettings tercihlerine uyar:
- NotifyFollow → 9
- NotifyMatchInvite → 10
- NotifyTeamInvite → 11
- NotifyMatchReminder → 6
- NotifyMatchResult → 8
- NotifyMatchUpdates → 1, 2, 3, 5, 7

Tercihi kapalı olan oyuncuya bildirim satırı oluşturulmaz.

## DTO'lar
**SocialPlayerDto** = PublicPlayerDto alanları + `iFollow`, `followsMe`, `isMutual` (bool), `playedTogether` (int: birlikte oynanan maç sayısı).

**PlayerProfileDto**
```json
{
  "player": { /* PublicPlayerDto */ },
  "followersCount": 12, "followingCount": 8,
  "iFollow": true, "followsMe": false, "isMutual": false,
  "playedTogether": 3,
  "canMessage": true,               // ben bu kişiye mesaj atabilir miyim
  "isBlockedByMe": false,
  "isMe": false,
  "statsVisible": true,             // ProfileVisibility kuralına göre
  "stats": { /* GetPlayerStats şekli */ } | null,
  "teams": [{ "teamId": 1, "teamName": "..." }]
}
```
`statsVisible`:
- 0 → herkes
- 1 → ben onu takip ediyorsam ya da kendi profilimse
- 2 → sadece kendisi

`statsVisible` false ise `stats` ve `teams` null döner.

## Uç noktalar — `/api/Social/...`
| Metot | Yol | Gövde / Query | Dönüş |
|---|---|---|---|
| POST | Follow | `{ playerId }` | `{ success, message, isMutual }`. Kendini takip edemez, engel → 403. Hedefe NewFollower bildirimi; Activity 6 |
| POST | Unfollow | `{ playerId }` | `{ success }` |
| GET | Followers | `?playerId=` (boş = ben) | `SocialPlayerDto[]`. Başkasının listesi ProfileVisibility kuralına tabi (gizliyse 403) |
| GET | Following | `?playerId=` | `SocialPlayerDto[]` |
| GET | Discover | `?q=&take=30` | `SocialPlayerDto[]`. q varsa ad/soyad/kullanıcı adı araması. Ben, adminler, silinmişler ve engeller hariç |
| GET | Suggestions | `?take=10` | `SocialPlayerDto[]`. Sıra: (1) birlikte oynadığım ama takip etmediğim, playedTogether azalan; (2) beni takip eden ama geri takip etmediğim; (3) takip ettiklerimin takip ettikleri |
| GET | Profile | `?playerId=` | `PlayerProfileDto` (engel → 404) |
| POST | Block | `{ playerId }` | `{ success }`. Takipler iki yönlü silinir, bekleyen davetler iptal (red) |
| POST | Unblock | `{ playerId }` | |
| GET | Blocked | | `PublicPlayerDto[]` |
| POST | Report | `{ playerId, reason, details? }` | `{ success }`. reason: "spam", "hakaret", "sahte-hesap", "maca-gelmeme", "diger" |
| GET | Settings | | PlayerSettings alanları (camelCase, PlayerId hariç) |
| POST | Settings | aynı alanlar | `{ success }`. Değer aralıkları doğrulanır |
| POST | DeleteAccount | `{ password }` | `{ success }` |
| GET | MessageContacts | | Aşağıya bakın |
| GET | Feed | `?take=30&before=<ISO tarih>` | `ActivityDto[]` |

**DeleteAccount:**
- Şifre doğrulanır.
- Follows, Blocks, PlayerSettings ve davetler silinir.
- Gelecek maçlardaki katılımlar Leave mantığıyla bırakılır (yedekten yükseltme dahil).
- Player anonimleştirilir: PlayerName "Silinmiş", PlayerSurname "Kullanıcı", UserName `silinmis_<id>`, Email/Phone/Address boş, ProfilePictureUrl null, Password rastgele hash, IsDeleted true, DeletedAt now.
- Geçmiş maç kayıtları korunur.

**MessageContacts** (mesaj panelinin "Kişiler" listesi)
```json
[{ "player": { /* SocialPlayerDto */ },
   "lastMessage": { "text": "...", "sendingDate": "...", "mine": true } | null }]
```
İçerik: yazıştığım herkes + karşılıklı takipleştiklerim (engeller ve silinmişler hariç).
Sıra: son mesaj tarihi azalan, mesajı olmayanlar sonda ve ada göre.

**ActivityDto** (Feed): takip ettiğim oyuncuların aktiviteleri (engeller hariç), CreatedAt azalan.
```json
{ "activityId": 1, "type": 3, "createdAt": "...",
  "actor": { /* PublicPlayerDto */ },
  "match": { "matchId": 1, "matchName": "...", "matchDate": "...", "homeScore": 3, "awayScore": 2, "stadiumName": "..." } | null,
  "team": { "teamId": 1, "teamName": "..." } | null,
  "target": { /* PublicPlayerDto */ } | null,
  "value": 3 }
```

## Mesaj izni (MessageController değişikliği)
`CanMessage(sender, recipient)`:
- Engel (iki yön) → hayır.
- Recipient.MessagePermission = 0 → evet.
- = 1 → isMutual.
- = 2 → isMutual ya da birlikte oynamışlar.
- Ayrıca daha önce yazışmışlarsa (recipient → sender en az bir mesaj) her zaman evet.

`MessageSave` izin yoksa **403** `{ message: "Bu oyuncu sadece arkadaşlarından mesaj kabul ediyor." }` döner. Grup mesajları etkilenmez.

## Davetler — `/api/Invites/...`
| Metot | Yol | Gövde | Kural |
|---|---|---|---|
| POST | InviteToMatch | `{ matchId, playerIds: [] }` | Davet eden maçta Going ya da canManage olmalı. Maç geçmiş/reddedilmiş → 400. Engelliler, zaten katılımcı (Going/Waitlist) olanlar ve zaten bekleyen daveti olanlar atlanır. Dönüş `{ success, invited: n, skipped: n }` |
| POST | InviteToTeam | `{ teamId, playerIds: [] }` | Davet eden takımın üyesi olmalı. Zaten üye olanlar atlanır |
| GET | My | | `{ "matchInvites": [MatchInviteDto], "teamInvites": [TeamInviteDto] }`, sadece bekleyenler, yeniden eskiye |
| GET | PendingCount | | `{ "count": 3 }` |
| POST | Respond | `{ "kind": "match" \| "team", inviteId, accept }` | Sadece davetli. Kabul (match) → Join mantığı (Going/Waitlist), dönüş `{ success, message, status }`. Kabul (team) → PlayerTeam eklenir. Red → Status 2. Geçmiş maç daveti kabul edilemez (400) |

`MatchInviteDto`: `{ inviteId, createdAt, inviter: PublicPlayerDto, match: { matchId, matchName, matchDate, stadiumName, capacity, goingCount, isActive } }`
`TeamInviteDto`: `{ inviteId, createdAt, inviter: PublicPlayerDto, team: { teamId, teamName, memberCount } }`

## Takvim — `/api/Match/MyCalendar?from=&to=`
from/to ISO tarih; boşsa bu ayın başından 2 ay sonrasına kadar.
```json
[{ "matchId": 1, "matchName": "...", "matchDate": "...", "stadiumName": "...",
   "kind": "going" | "waitlist" | "invited", "isActive": 2, "capacity": 10, "goingCount": 7,
   "homeScore": null, "awayScore": null }]
```

## Mevcut uç noktalara eklemeler
- `GET /api/Match/GetMatchFeed`: her maça `followingGoingCount` (int) ve `followingPreview` (PublicPlayerDto[], en fazla 3, Going olan ve benim takip ettiklerim) eklenir. Engellediklerim/beni engelleyenler `participantsPreview`'da görünmez.
- `GET /api/Match/GetMatchDetail`: her participant'a `iFollow` (bool) eklenir.
- `GET /api/Players/GetPlayerList`, `GetLeaderboard`: silinmiş hesaplar ve engeller (iki yön) hariç.
- `PlayerLogin`: silinmiş hesap → 401.
- `SaveTeam`: Activity 5 yazılır.

## Admin — `/api/Admin/...` (`[Authorize(Roles = Admin)]`)
| Metot | Yol | Dönüş |
|---|---|---|
| GET | Reports | `?status=0` → `[{ reportId, reason, details, status, createdAt, reporter: PublicPlayerDto, reported: PublicPlayerDto, reportedCount: n }]` (reportedCount = o oyuncu hakkındaki toplam şikayet) |
| POST | ResolveReport | `{ reportId }` → Status 1 |
