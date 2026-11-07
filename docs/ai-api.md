# AI API: maç önerisi ve takım dengeleme

Eski Python servisleri (`ai_match_sorter.py`, `GetAPIPlayers.py`, portlar 5000/5001) kaldırıldı. Aynı işler artık API içinde,
aynı JWT ve veritabanıyla çalışır; ayrıca bir servis başlatmak gerekmez. Kod: `Services/Ai/RecommendationService.cs`.

## GET `/api/Ai/MatchRecommendations`

Giriş yapan oyuncu için katılabileceği (yeri olan, iptal edilmemiş, kendisinin kurmadığı, henüz katılmadığı) yaklaşan maçlar, puana göre sıralı.

```json
[{ "matchId": 4279, "score": 83.8, "reason": "friends", "args": { "count": 5 } }]
```

Puan (0–100) = 0.35·tarih yakınlığı + 0.25·tanıdık oyuncular (takip ettikleri ve son 50 maçta birlikte oynadıkları)
+ 0.15·seviye uyumu (UserScore) + 0.10·tesis tanıdıklığı + 0.15·doluluk/beğeni.

`reason` (frontend `home:ai.reasons.<reason>` ile çevirir): `friends {count}`, `lastSpots {count}`, `venue {venue,count}`,
`soon`, `level`, `popular {count}`, `open {count}`.

## POST `/api/Ai/BalanceTeams`

```json
{ "matchId": 4284, "apply": false }          // maçta "katılıyor" olan oyuncular
{ "playerIds": [1199, 1193, 1200, 1198] }    // seçilen oyuncular
{}                                            // ben + takip ettiklerim
```

Yanıt: `{ team1: [...], team2: [...], power1, power2 }`. Her oyuncuda `power` (0–100) ve `rating` (maç sonrası puan ortalaması, 1–5).

- Güç = 0.75·beceri (oyuncu puanı ortalaması, yoksa UserScore) + 0.15·yaş + 0.10·boy.
- Güçlüden zayıfa, toplamı düşük takıma dağıtılır; takımlar eşit sayıda tutulur ve tek takaslarla fark küçültülür.
- `apply: true` + `matchId`: sonuç maçın taraflarına (ev/deplasman) yazılır. Yalnızca maçı kuran ya da admin (`MATCH_ORGANIZER_ONLY`).
- Maçı görüntüleme: kurucu, admin ya da maçta katılımcı olan (`MATCH_NOT_PARTICIPANT`).
- Oyuncu sayısı 2–40 (`MATCH_BALANCE_PLAYER_COUNT`).
