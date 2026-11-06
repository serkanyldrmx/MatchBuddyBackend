# Turnuvalar / Ligler — API Sözleşmesi

Kalıcı takımlar (`Teams` + `PlayerTeam`) turnuvaya katılır. İki format desteklenir:
- **Lig:** tek devreli, herkes herkesle; puan tablosu.
- **Eleme:** tek maç, kazanan üst tura çıkar; ağaç (bracket).

Tüm uç noktalar `[Authorize]`, JSON camelCase.
Hata gövdesi: `{ success:false, message }` (400/403/404).
"Takım üyesi" = PlayerTeam kaydı olan oyuncu.
**canManage:** admin veya turnuvayı oluşturan.

## Veri modeli
| Tablo | Alanlar |
|---|---|
| Tournaments | TournamentId PK, Name nvarchar(80), Description nvarchar(500) null, Format byte (1 lig, 2 eleme), Status byte (0 kayıt açık, 1 devam ediyor, 2 tamamlandı), StartDate datetime, MaxTeams int (4–16), StadiumId int? FK, CreatedByPlayerId FK (NoAction), WinnerTeamId int? FK (NoAction), CreatedAt |
| TournamentTeams | TournamentTeamId PK, TournamentId FK (Cascade), TeamId FK (NoAction), JoinedAt. Unique (TournamentId, TeamId) |
| TournamentFixtures | FixtureId PK, TournamentId FK (Cascade), Round int (1'den başlar), Position int (turdaki sıra, 0'dan), HomeTeamId int? (NoAction), AwayTeamId int? (NoAction), MatchDate datetime?, HomeScore int?, AwayScore int?, WinnerTeamId int? (NoAction), Status byte (0 oynanmadı, 1 oynandı, 2 bay geçti) |

Team silinirken turnuvada kaydı varsa `DeleteTeam` 400 döner ("Takım bir turnuvada yer alıyor").

## Kurallar
- **Oluşturma:** herkes. Status 0.
- **Takım kaydı:** Status 0 iken; kaydı yapan o takımın üyesi olmalı, kontenjan dolmamış olmalı, takım daha önce eklenmemiş olmalı.
- **Kayıttan çekilme:** Status 0 iken; takım üyesi veya canManage.
- **Başlatma (canManage, Status 0, en az 2 takım; lig için en az 3):**
  - **Lig:** circle method ile tek devre fikstür üretilir. Takım sayısı tekse her turda bir takım bay geçer; bay için fikstür satırı oluşturulmaz. Tur r'nin tarihi `StartDate + 7*(r-1) gün`, saat StartDate'in saati.
  - **Eleme:**
    - Takımlar rastgele karıştırılır; boyut bir sonraki 2'nin kuvvetine (2, 4, 8, 16) tamamlanır.
    - Eksik yerler "bay" olur: rakibi null olan fikstür Status 2 alır, takım otomatik üst tura geçer (WinnerTeamId set edilir).
    - Bütün turların fikstür satırları baştan oluşturulur: tur 1'de n/2 maç, sonraki turlar boş takımlarla.
    - Tur r, pozisyon p'nin kazananı tur r+1, pozisyon p/2'ye gider; çift p ise Home, tek p ise Away.
  - Status 1 olur. Kayıtlı takımların üyelerine bildirim gönderilir.
- **Sonuç girme (canManage, Status 1, fikstürün iki takımı da belli):**
  - Skor 0–50. Tekrar girilebilir (düzeltme).
  - Elemede beraberlik varsa `winnerTeamId` zorunlu (penaltılar). Kazanan üst tura yerleştirilir. Düzeltmede üst tur maçı henüz oynanmadıysa kazanan değiştirilebilir, oynandıysa 400.
  - **Turnuva biter (Status 2, WinnerTeamId):**
    - Lig: tüm fikstürler oynandığında puan tablosunun lideri kazanır.
    - Eleme: final oynandığında finalin kazananı.
  - Bitişte kazanan takımın her üyesi için Activity Type 7 (TournamentWon) yazılır (TeamId dolu); bütün katılımcı takım üyelerine bildirim gider.
- **Puan tablosu (lig):** galibiyet 3, beraberlik 1, mağlubiyet 0. Sıralama: puan, averaj, atılan gol, takım adı.
- **Fikstür tarihi güncelleme:** canManage.
- **Silme:** canManage ve Status 0.

`NotificationType` sona eklenir: `TournamentUpdate = 12` (alıcı tercihi: NotifyMatchUpdates).

## DTO'lar
**TeamRefDto:** `{ teamId, teamName }`

**TournamentSummaryDto:**
```json
{
  "tournamentId": 1, "name": "Kış Ligi", "format": 1, "status": 0,
  "startDate": "...", "maxTeams": 8, "teamCount": 5,
  "stadium": { "stadiumId": 3, "stadiumName": "..." } | null,
  "createdBy": { /* PublicPlayerDto */ },
  "winner": { /* TeamRefDto */ } | null,
  "myTeamIds": [4]          // bu turnuvadaki, üyesi olduğum takımlar
}
```

**TournamentDetailDto** = Summary alanları +
```json
{
  "description": "...", "canManage": true,
  "teams": [{ "teamId": 4, "teamName": "...", "memberCount": 7,
              "members": [ /* PublicPlayerDto, en fazla 6 */ ], "isMine": true }],
  "fixtures": [{ "fixtureId": 9, "round": 1, "position": 0,
                 "homeTeam": { /* TeamRefDto */ } | null, "awayTeam": { /* TeamRefDto */ } | null,
                 "matchDate": "...", "homeScore": 2, "awayScore": 1,
                 "winnerTeamId": 4, "status": 1 }],
  "standings": [{ "team": { /* TeamRefDto */ }, "played": 3, "won": 2, "drawn": 1, "lost": 0,
                  "goalsFor": 7, "goalsAgainst": 3, "goalDiff": 4, "points": 7 }],  // sadece lig, diğerinde []
  "roundCount": 3,
  "myEligibleTeams": [ /* TeamRefDto */ ]   // üyesi olduğum ve henüz kayıtlı olmayan takımlar (kayıt açıkken)
}
```

## Uç noktalar — `/api/Tournaments/...`
| Metot | Yol | Gövde / Query | Dönüş |
|---|---|---|---|
| GET | List | `?status=` (boş = hepsi) | `TournamentSummaryDto[]` (StartDate'e göre: önce devam eden, sonra kayıt açık, sonra tamamlanan) |
| GET | Detail | `?tournamentId=` | `TournamentDetailDto` |
| POST | Create | `{ name, description, format, startDate, maxTeams, stadiumId? }` | `{ success, message, tournamentId }` |
| POST | RegisterTeam | `{ tournamentId, teamId }` | `{ success, message }` |
| POST | WithdrawTeam | `{ tournamentId, teamId }` | |
| POST | Start | `{ tournamentId }` | |
| POST | SubmitResult | `{ fixtureId, homeScore, awayScore, winnerTeamId? }` | |
| POST | UpdateFixtureDate | `{ fixtureId, matchDate }` | |
| POST | Delete | `{ tournamentId }` | |
