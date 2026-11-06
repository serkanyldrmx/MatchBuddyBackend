# Platform API: hata modeli, yerelleştirme, parametreler, işletme/oyuncu ayarları

This document is the contract for the **platform layer** of the MatchBuddy backend:

- the standard error body and the error-code catalog
- localization
- system parameters, venue settings and player preferences
- the proxy / Swagger / logging / health configuration

Other docs (`media-api.md`, `social-api.md`, `venues-api.md`, `match-participation-api.md`, `tournaments-api.md`) still describe their own endpoints. **Every error they return now uses the body described here.**

Base URL: `/api`. JSON uses camelCase.

---

## 1. Standart hata gövdesi

All error responses (4xx/5xx) use the same JSON shape. This covers controllers, model validation, JWT 401/403, unknown routes (404/405), rate limiting (429) and unhandled errors (500).

```json
{
  "success": false,
  "code": "MATCH_NOT_FOUND",
  "message": "Maç bulunamadı.",
  "errors": { "pitchId": ["'abc' değeri pitchId alanı için geçersiz."] },
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736"
}
```

| Alan | Tür | Açıklama |
|---|---|---|
| `success` | bool | Always `false`. |
| `code` | string | Stable, machine-readable code from the catalog in §3. **The frontend should branch on this, not on `message`.** |
| `message` | string | Human-readable text in the request language (§2). Show it to the user directly. Some messages carry runtime values (limits, counts). |
| `errors` | object? | Only present for field-level errors: `{ "<alan>": ["mesaj", …] }`. Keys are camelCase. Nested fields use dots (`owner.userName`). A body-level problem uses `body`. |
| `traceId` | string | Request trace id; the same value is in the `X-Trace-Id` response header and in the server logs. |
| `detail` | string? | **Development only:** exception details on a 500. Never present in other environments. |

**Success responses are unchanged.** Endpoints that used to return `{ success: true, message }` still do, and the `message` now comes from the resource file. Arrays and DTOs are the same as before.

Notes for clients:
- Some old endpoints returned a plain string or `{ success, message, data }` on error. They now all return the body above.
- The old `field` property (e.g. `"field": "photos"`) is replaced by `errors: { "photos": [message] }`.
- A failed legacy `Result` no longer carries `data: null` in its error body.

### HTTP durumu → kod

| HTTP | Ne zaman | `code` |
|---|---|---|
| 400 | Business rule / input error | Area code (`VENUE_*`, `MATCH_*` …) |
| 400 | Automatic model validation (wrong type, unreadable/empty JSON, required fields) | `VALIDATION_FAILED` + `errors` |
| 401 | No token, or the token is invalid or expired (JWT) | `AUTH_UNAUTHORIZED` (`WWW-Authenticate: Bearer` header) |
| 401 | Wrong user name or password (`PlayerLogin`) | `AUTH_INVALID_CREDENTIALS` |
| 403 | Role / ownership not sufficient | `AUTH_FORBIDDEN` or an area code (`VENUE_NOT_YOURS`, `VENUE_MEMBERSHIP_INACTIVE`, `MATCH_OWNER_CANNOT_JOIN` …) |
| 404 | Unknown route | `COMMON_NOT_FOUND` |
| 404 | Record not found | Area code (`MATCH_NOT_FOUND`, `VENUE_NOT_FOUND`, `PARAM_NOT_FOUND` …) |
| 405 | Wrong HTTP method | `COMMON_METHOD_NOT_ALLOWED` |
| 409 | Concurrent change (reserved) | `COMMON_CONFLICT` |
| 413 / 415 | Body too large / unsupported content type | `COMMON_PAYLOAD_TOO_LARGE` / `COMMON_UNSUPPORTED_MEDIA_TYPE` |
| 429 | Rate limit (§7.4) | `COMMON_RATE_LIMITED` + `Retry-After` header (seconds) |
| 500 | Unhandled error | `COMMON_INTERNAL_ERROR` |
| 502 | OpenStreetMap unreachable (admin OSM search) | `OSM_*` |
| 503 | `/health` unhealthy (its own body, see §7.5) | — |

### Örnekler

```http
GET /api/Social/Settings            (no token)
401  { "success": false, "code": "AUTH_UNAUTHORIZED", "message": "Oturumunuz sona erdi veya giriş yapmadınız. Lütfen tekrar giriş yapın.", "traceId": "…" }

POST /api/Venues/Quote  { "pitchId": "abc" }
400  { "success": false, "code": "VALIDATION_FAILED",
       "message": "Gönderilen bilgiler geçersiz. Lütfen işaretli alanları kontrol edin.",
       "errors": { "pitchId": ["Gönderilen değer okunamadı (geçersiz biçim)."] }, "traceId": "…" }

PUT /api/Admin/Parameters/venue.maxPhotos  { "value": "1000" }
400  { "success": false, "code": "PARAM_OUT_OF_RANGE", "message": "'venue.maxPhotos' parametresi 1 ile 100 arasında olmalıdır.",
       "errors": { "value": ["'venue.maxPhotos' parametresi 1 ile 100 arasında olmalıdır."] }, "traceId": "…" }

POST /api/Players/PlayerLogin  (11th attempt within a minute)
429  Retry-After: 37
     { "success": false, "code": "COMMON_RATE_LIMITED", "message": "Çok fazla istek. Lütfen biraz sonra tekrar deneyin.", "traceId": "…" }
```

### Yanıt başlıkları (tüm yanıtlar)

| Başlık | Değer |
|---|---|
| `X-Trace-Id` | Same value as `traceId` (exposed via CORS) |
| `X-Content-Type-Options` | `nosniff` |
| `Referrer-Policy` | `no-referrer` |
| `X-Frame-Options` | `DENY` |
| `Content-Security-Policy` | `frame-ancestors 'none'` |
| `Content-Language` | Language used for the response (`tr`, `en`) |
| `Retry-After` | Only on 429 (exposed via CORS) |

---

## 2. Yerelleştirme (localization)

- **Source:** every user-facing text comes from `MatchBuddy.Core/Resources/Messages.resx`. That file is the neutral resource and is **Turkish** (`[assembly: NeutralResourcesLanguage("tr")]`). This covers:
  - error and success messages
  - notification texts
  - field labels
  - parameter descriptions
- **Other languages:** they live in `Messages.<dil>.resx` satellite files. `Messages.en.resx` currently exists and holds the `COMMON_*`, `VALIDATION_*`, `AUTH_*`, `PARAM_*` and common `*_NOT_FOUND` texts. Any key missing from it falls back to Turkish.
- **Keys:**
  - Errors use the code itself, e.g. `MATCH_NOT_FOUND`.
  - Other texts use these prefixes: `MSG_*` (success/info), `NOTIF_*` (notification text), `LABEL_*` (field/label name), `PARAMDESC_*` (parameter description).
- **Placeholders:** texts use `string.Format` placeholders (`{0}`, `{1:dd.MM.yyyy HH:mm}`), and the values are filled in on the server.
- **Language selection:** this is done per request by the `RequestLocalization` middleware, in this order:
  1. `?culture=en` query parameter (for tests / debugging)
  2. the `Accept-Language` header
  3. otherwise the default (`Localization:DefaultCulture`, `tr`)

  Unsupported languages (e.g. `de-DE`) get the default. `en-US` falls back to `en`. Cookies are not used.
- **Frontend:**
  - Send `Accept-Language: <PlayerSettings.language>` (§6) on every request. Before login, send the browser language or `tr`.
  - Show messages straight from `message`.
  - Use `code` for special handling: for example, on `AUTH_UNAUTHORIZED` redirect to the login page.
- **Notifications:** notification texts (`Notification.Text`) are stored in the database and shared by several recipients. They are therefore written in the **default language** (`Msg.GetDefault`), not in the language of the request that triggered them.
- Background services (match reminders, media cleanup) also use the default language.

### Yeni dil eklemek
1. Copy `MatchBuddy.Core/Resources/Messages.resx` to `Messages.<kod>.resx` (e.g. `Messages.de.resx`). Translate the `value`s and keep the placeholders (`{0}` …) unchanged. Keys you leave out fall back to Turkish.
2. Add the code to `appsettings.json` → `Localization:SupportedCultures` (e.g. `["tr","en","de"]`).
3. Rebuild; the satellite assembly is produced automatically (`bin/.../<kod>/MatchBuddy.Core.resources.dll`).

   `PlayerSettings.language` only accepts the languages in this list (`SOCIAL_INVALID_LANGUAGE`).

Developer rules:
- New messages are added only to the resx and to the `ErrorCodes` / `MessageKeys` catalogs, never written as literals in code.
- In code, use `OperationResult<T>.BadRequest(ErrorCodes.X.Y, args…)` or `Msg.Get(MessageKeys.X.Y, args…)`.

---

## 3. Hata kodu kataloğu

Constants: `MatchBuddy.Core/Errors/ErrorCodes*.cs` (`ErrorCodes.Common`, `.Validation`, `.Auth`, `.Match`, `.Venue` …). The constant value is the same string as the resx key. The Turkish texts below are the current contents of `Messages.resx`, and the frontend gets them in `message`.

The `VALIDATION_*` texts (other than `VALIDATION_FAILED`) also appear inside the `errors` dictionary. Codes whose placeholders say "alan adı" get a localized field label as an argument.


#### COMMON_*

| Kod | Mesaj (tr) | Yer tutucular |
|---|---|---|
| `COMMON_BAD_REQUEST` | İstek geçersiz. |  |
| `COMMON_CONFLICT` | Kayıt başka bir işlemle değiştirildi. Lütfen sayfayı yenileyip tekrar deneyin. |  |
| `COMMON_INTERNAL_ERROR` | Beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyin. |  |
| `COMMON_INVALID_DATE_RANGE` | Bitiş tarihi başlangıçtan sonra olmalıdır. |  |
| `COMMON_INVALID_JSON` | Geçersiz JSON formatı. |  |
| `COMMON_MAINTENANCE` | Sistem Bakımda |  |
| `COMMON_METHOD_NOT_ALLOWED` | Bu işlem bu adres için desteklenmiyor. |  |
| `COMMON_NOT_FOUND` | Aradığınız kaynak bulunamadı. |  |
| `COMMON_OPERATION_FAILED` | İşlem gerçekleştirilemedi. |  |
| `COMMON_PAYLOAD_TOO_LARGE` | Gönderilen veri çok büyük. |  |
| `COMMON_RATE_LIMITED` | Çok fazla istek. Lütfen biraz sonra tekrar deneyin. |  |
| `COMMON_SERVICE_UNAVAILABLE` | Hizmet şu anda kullanılamıyor. Lütfen daha sonra tekrar deneyin. |  |
| `COMMON_UNSUPPORTED_MEDIA_TYPE` | Gönderilen içerik türü desteklenmiyor. |  |
| `COMMON_UPSTREAM_ERROR` | Dış servise ulaşılamadı. Lütfen daha sonra tekrar deneyin. |  |

#### VALIDATION_*

| Kod | Mesaj (tr) | Yer tutucular |
|---|---|---|
| `VALIDATION_BODY_REQUIRED` | İstek gövdesi boş olamaz. |  |
| `VALIDATION_FAILED` | Gönderilen bilgiler geçersiz. Lütfen işaretli alanları kontrol edin. |  |
| `VALIDATION_INVALID_EMAIL` | Geçerli bir e-posta adresi girin. |  |
| `VALIDATION_INVALID_FORMAT` | Gönderilen değer okunamadı (geçersiz biçim). |  |
| `VALIDATION_INVALID_VALUE` | '{0}' değeri {1} alanı için geçersiz. | {0}: değer, {1}: alan adı |
| `VALIDATION_MUST_BE_NUMBER` | {0} alanı sayı olmalıdır. | {0}: alan adı |
| `VALIDATION_NEGATIVE_NOT_ALLOWED` | {0} negatif olamaz. | {0}: alan adı |
| `VALIDATION_OUT_OF_RANGE` | {0} {1} ile {2} arasında olmalıdır. | {0}: alan adı, {1}: en az, {2}: en fazla |
| `VALIDATION_REQUIRED` | {0} alanı zorunludur. | {0}: alan adı |
| `VALIDATION_TOO_LONG` | {0} en fazla {1} karakter olabilir. | {0}: alan adı, {1}: en fazla |
| `VALIDATION_TOO_SHORT` | {0} en az {1} karakter olmalıdır. | {0}: alan adı, {1}: en az |
| `VALIDATION_UNKNOWN_VALUE_INVALID` | Girilen değer geçersiz. |  |
| `VALIDATION_VALUE_INVALID` | '{0}' değeri geçersiz. | {0}: değer |
| `VALIDATION_VALUE_MISSING` | Bir değer girilmelidir. |  |

#### AUTH_*

| Kod | Mesaj (tr) | Yer tutucular |
|---|---|---|
| `AUTH_ADMIN_ONLY` | Bu işlem yalnızca yöneticiler içindir. |  |
| `AUTH_FORBIDDEN` | Bu işlem için yetkiniz yok. |  |
| `AUTH_INVALID_CREDENTIALS` | Kullanıcı adı veya şifre hatalı. |  |
| `AUTH_PASSWORD_REQUIRED` | Şifre gerekli. |  |
| `AUTH_PASSWORD_TOO_SHORT` | Şifre en az {0} karakter olmalıdır. | {0}: en az karakter sayısı |
| `AUTH_PASSWORD_WRONG` | Şifre hatalı. |  |
| `AUTH_UNAUTHORIZED` | Oturumunuz sona erdi veya giriş yapmadınız. Lütfen tekrar giriş yapın. |  |
| `AUTH_USERNAME_REQUIRED` | Kullanıcı adı zorunludur. |  |
| `AUTH_USERNAME_TAKEN` | Bu kullanıcı adı zaten kullanılıyor. |  |

#### PARAM_*

| Kod | Mesaj (tr) | Yer tutucular |
|---|---|---|
| `PARAM_INVALID_JSON` | Parametre değeri geçerli bir JSON olmalıdır. |  |
| `PARAM_INVALID_LIST` | '{0}' parametresi virgülle ayrılmış sayı listesi olmalıdır (ör. 5,6,7). | {0}: anahtar |
| `PARAM_INVALID_TYPE` | '{0}' parametresi için değer {1} türünde olmalıdır. | {0}: anahtar, {1}: tür |
| `PARAM_NOT_FOUND` | Parametre bulunamadı. |  |
| `PARAM_OUT_OF_RANGE` | '{0}' parametresi {1} ile {2} arasında olmalıdır. | {0}: anahtar, {1}: en az, {2}: en fazla |
| `PARAM_VALUE_REQUIRED` | Parametre değeri boş olamaz. |  |

#### MATCH_*

| Kod | Mesaj (tr) | Yer tutucular |
|---|---|---|
| `MATCH_ALREADY_JOINED` | Bu maça zaten katılıyorsun. |  |
| `MATCH_ALREADY_WAITLISTED` | Zaten bu maçın yedek listesindesin. |  |
| `MATCH_CALENDAR_INVALID_RANGE` | Bitiş tarihi başlangıçtan önce olamaz. |  |
| `MATCH_CONFIRM_ONLY_GOING` | Sadece maça katılan (asil) oyuncular onay verebilir. |  |
| `MATCH_GUEST_NAME_LENGTH` | Misafir adı {0}–{1} karakter olmalıdır. | {0}: en az, {1}: en fazla karakter |
| `MATCH_GUEST_NOT_FOUND` | Misafir oyuncu bulunamadı. |  |
| `MATCH_INVALID_SIDE` | Taraf 1 (ev sahibi) veya 2 (deplasman) olmalıdır. |  |
| `MATCH_MVP_NOT_OPEN` | MVP oylaması maç oynandıktan sonra açılır. |  |
| `MATCH_MVP_SELF_VOTE` | Kendine oy veremezsin. |  |
| `MATCH_MVP_VOTED_NOT_PLAYED` | Oy verilen oyuncu bu maçta oynamadı. |  |
| `MATCH_MVP_VOTER_NOT_PLAYED` | Sadece maçta oynayan oyuncular oy verebilir. |  |
| `MATCH_NAME_INVALID` | Maç ismi 5 Karakterden Küçük Olamaz |  |
| `MATCH_NOT_FOUND` | Maç bulunamadı. |  |
| `MATCH_NOT_PARTICIPANT` | Bu maçta kaydın bulunmuyor. |  |
| `MATCH_OWNER_CANNOT_CREATE` | İşletme hesapları maç kuramaz. |  |
| `MATCH_OWNER_CANNOT_JOIN` | İşletme hesapları maça katılamaz. |  |
| `MATCH_PAST_CANNOT_LEAVE` | Maç tarihi geçti, kadrodan ayrılamazsın. |  |
| `MATCH_PAST_JOIN_CLOSED` | Maç tarihi geçti, katılım kapalı. |  |
| `MATCH_PAST_ROSTER_LOCKED` | Maç tarihi geçti, kadro değiştirilemez. |  |
| `MATCH_PLAYER_NOT_IN_ROSTER` | Oyuncu bu maçta asil kadroda değil. |  |
| `MATCH_RATED_NOT_PLAYED` | Puanlanan oyuncular bu maçta oynamış olmalı. |  |
| `MATCH_RATER_NOT_PLAYED` | Sadece maçta oynayan oyuncular puan verebilir. |  |
| `MATCH_RATINGS_REQUIRED` | En az bir oyuncuya puan vermelisin. |  |
| `MATCH_RATING_NOT_OPEN` | Puanlama maç oynandıktan sonra açılır. |  |
| `MATCH_RATING_SELF` | Kendine puan veremezsin. |  |
| `MATCH_REJECTED_JOIN_CLOSED` | Bu maç reddedildi, katılım kapalı. |  |
| `MATCH_REJECTED_ROSTER_LOCKED` | Bu maç reddedildi, kadro değiştirilemez. |  |
| `MATCH_RESULT_NOT_PLAYED` | Maç henüz oynanmadı, sonuç girilemez. |  |
| `MATCH_SIDE_FULL` | Bu tarafta boş yer yok. |  |
| `MATCH_TEAM_ALREADY_ADDED` | Bu takım zaten maça ekli. |  |
| `MATCH_TIME_CONFLICT` | Aynı stadyumda aynı saatte başka bir maç bulunmaktadır. |  |

#### COMMENT_*

| Kod | Mesaj (tr) | Yer tutucular |
|---|---|---|
| `COMMENT_EMPTY` | Yorum boş olamaz. |  |
| `COMMENT_NOT_FOUND` | Yorum bulunamadı. |  |

#### NOTIFICATION_*

| Kod | Mesaj (tr) | Yer tutucular |
|---|---|---|
| `NOTIFICATION_NOT_FOUND` | Bildirim bulunamadı. |  |

#### PLAYER_*

| Kod | Mesaj (tr) | Yer tutucular |
|---|---|---|
| `PLAYER_INVALID_DATA` | Geçersiz oyuncu bilgisi. |  |
| `PLAYER_NAME_INVALID` | Oyuncu ismi 3 Karakterden Küçük Olamaz |  |
| `PLAYER_NOT_FOUND` | Oyuncu bulunamadı. |  |

#### SOCIAL_*

| Kod | Mesaj (tr) | Yer tutucular |
|---|---|---|
| `SOCIAL_CANNOT_BLOCK_SELF` | Kendini engelleyemezsin. |  |
| `SOCIAL_CANNOT_FOLLOW_SELF` | Kendini takip edemezsin. |  |
| `SOCIAL_CANNOT_REPORT_SELF` | Kendini şikayet edemezsin. |  |
| `SOCIAL_FOLLOW_BLOCKED` | Bu oyuncuyu takip edemezsin. |  |
| `SOCIAL_INVALID_LANGUAGE` | Dil şunlardan biri olmalıdır: {0}. | {0}: desteklenen diller |
| `SOCIAL_INVALID_REPORT_REASON` | Geçersiz şikayet nedeni. Geçerli değerler: {0}. | {0}: virgülle ayrılmış geçerli nedenler |
| `SOCIAL_INVALID_SETTING_CHOICE` | {0} 0, 1 veya 2 olmalıdır. | {0}: alan adı (messagePermission / profileVisibility) |
| `SOCIAL_INVALID_THEME` | Tema şunlardan biri olmalıdır: {0}. | {0}: izinli değerler |
| `SOCIAL_LIST_PRIVATE` | Bu oyuncunun listesi gizli. |  |
| `SOCIAL_OWNER_CANNOT_FOLLOW` | İşletme hesapları takip yapamaz. |  |
| `SOCIAL_OWNER_NOT_FOLLOWABLE` | İşletme hesapları takip edilemez. |  |
| `SOCIAL_REPORT_NOT_FOUND` | Şikayet bulunamadı. |  |
| `SOCIAL_SETTINGS_REQUIRED` | Ayarlar boş olamaz. |  |

#### INVITE_*

| Kod | Mesaj (tr) | Yer tutucular |
|---|---|---|
| `INVITE_ALREADY_RESPONDED` | Bu davet zaten yanıtlandı. |  |
| `INVITE_BATCH_LIMIT` | Tek seferde en fazla {0} oyuncu davet edilebilir. | {0}: en fazla oyuncu sayısı |
| `INVITE_INVALID_KIND` | kind 'match' veya 'team' olmalıdır. |  |
| `INVITE_MATCH_PAST` | Maç tarihi geçti, davet gönderilemez. |  |
| `INVITE_MATCH_REJECTED` | Bu maç reddedildi, davet gönderilemez. |  |
| `INVITE_NOT_FOUND` | Davet bulunamadı. |  |
| `INVITE_NOT_PLAYING_IN_MATCH` | Davet göndermek için maçta oynuyor olmalısın. |  |
| `INVITE_NOT_TEAM_MEMBER` | Davet göndermek için takımın üyesi olmalısın. |  |
| `INVITE_NOT_YOURS` | Bu davet sana gönderilmedi. |  |
| `INVITE_NO_PLAYERS` | Davet edilecek en az bir oyuncu seçmelisin. |  |
| `INVITE_PAST_MATCH_ACCEPT` | Geçmiş maçın daveti kabul edilemez. |  |

#### MESSAGE_*

| Kod | Mesaj (tr) | Yer tutucular |
|---|---|---|
| `MESSAGE_BLOCKED` | Bu oyuncuyla mesajlaşamazsın. |  |
| `MESSAGE_EMPTY` | Mesaj boş olamaz. |  |
| `MESSAGE_NOT_ALLOWED` | Bu oyuncu sadece arkadaşlarından mesaj kabul ediyor. |  |

#### GROUP_*

| Kod | Mesaj (tr) | Yer tutucular |
|---|---|---|
| `GROUP_NAME_INVALID` | Grup ismi 3 Karakterden Küçük Olamaz |  |
| `GROUP_NOT_FOUND` | Grup bulunamadı. |  |

#### TEAM_*

| Kod | Mesaj (tr) | Yer tutucular |
|---|---|---|
| `TEAM_IN_TOURNAMENT` | Takım bir turnuvada yer alıyor. |  |
| `TEAM_NAME_INVALID` | Takım ismi 3 Karakterden Küçük Olamaz |  |
| `TEAM_NOT_FOUND` | Takım bulunamadı. |  |
| `TEAM_OWNER_CANNOT_CREATE` | İşletme hesapları takım kuramaz. |  |
| `TEAM_OWNER_NOT_ALLOWED` | İşletme hesapları takıma eklenemez. |  |
| `TEAM_PLAYER_ALREADY_IN_TEAM` | Oyuncu zaten bu takımda. |  |
| `TEAM_PLAYER_IDS_OR_TEAM_ID_REQUIRED` | PlayerId veya TeamId null olamaz. |  |

#### TOURNAMENT_*

| Kod | Mesaj (tr) | Yer tutucular |
|---|---|---|
| `TOURNAMENT_ALREADY_STARTED` | Turnuva zaten başladı. |  |
| `TOURNAMENT_BYE_HAS_NO_DATE` | Bay geçilen maçın tarihi olmaz. |  |
| `TOURNAMENT_DELETE_FORBIDDEN` | Turnuvayı sadece düzenleyen silebilir. |  |
| `TOURNAMENT_DELETE_ONLY_REGISTRATION` | Sadece kayıt aşamasındaki turnuva silinebilir. |  |
| `TOURNAMENT_DRAW_WINNER_REQUIRED` | Elemede beraberlikte kazanan takım (winnerTeamId) seçilmeli. |  |
| `TOURNAMENT_FIXTURE_DATE_FORBIDDEN` | Maç tarihini sadece turnuvanın yöneticisi değiştirebilir. |  |
| `TOURNAMENT_FIXTURE_NOT_FOUND` | Fikstür bulunamadı. |  |
| `TOURNAMENT_FIXTURE_TEAMS_UNDETERMINED` | Bu maçın iki takımı da henüz belli değil. |  |
| `TOURNAMENT_FULL` | Turnuva kontenjanı dolu. |  |
| `TOURNAMENT_INPUT_REQUIRED` | Turnuva bilgileri boş olamaz. |  |
| `TOURNAMENT_INVALID_FORMAT` | format 1 (lig) veya 2 (eleme) olmalıdır. |  |
| `TOURNAMENT_KNOCKOUT_MIN_TEAMS` | Eleme başlatmak için en az {0} takım gerekli. | {0}: en az takım sayısı |
| `TOURNAMENT_LEAGUE_MIN_TEAMS` | Lig başlatmak için en az {0} takım gerekli. | {0}: en az takım sayısı |
| `TOURNAMENT_MATCH_DATE_REQUIRED` | Maç tarihi gerekli. |  |
| `TOURNAMENT_NAME_REQUIRED` | Turnuva adı boş olamaz. |  |
| `TOURNAMENT_NOT_FOUND` | Turnuva bulunamadı. |  |
| `TOURNAMENT_REGISTER_NOT_TEAM_MEMBER` | Takımı kaydetmek için takımın üyesi olmalısın. |  |
| `TOURNAMENT_REGISTRATION_CLOSED` | Bu turnuvanın kayıtları kapandı. |  |
| `TOURNAMENT_RESULT_FORBIDDEN` | Sonucu sadece turnuvanın yöneticisi girebilir. |  |
| `TOURNAMENT_RESULT_NOT_ONGOING` | Sonuç sadece devam eden turnuvaya girilebilir. |  |
| `TOURNAMENT_SCORES_REQUIRED` | İki takımın skoru da girilmeli. |  |
| `TOURNAMENT_SCORE_OUT_OF_RANGE` | Skor {0}–{1} arasında olmalıdır. | {0}: en az skor, {1}: en fazla skor |
| `TOURNAMENT_STADIUM_NOT_FOUND` | Halı saha bulunamadı. |  |
| `TOURNAMENT_START_DATE_IN_PAST` | Başlangıç tarihi geçmişte olamaz. |  |
| `TOURNAMENT_START_DATE_REQUIRED` | Başlangıç tarihi gerekli. |  |
| `TOURNAMENT_START_FORBIDDEN` | Turnuvayı sadece düzenleyen başlatabilir. |  |
| `TOURNAMENT_TEAM_ALREADY_REGISTERED` | Takım bu turnuvaya zaten kayıtlı. |  |
| `TOURNAMENT_TEAM_COUNT_OUT_OF_RANGE` | Takım sayısı {0}–{1} arasında olmalıdır. | {0}: en az takım, {1}: en fazla takım |
| `TOURNAMENT_TEAM_NOT_REGISTERED` | Takım bu turnuvaya kayıtlı değil. |  |
| `TOURNAMENT_WINNER_LOCKED` | Üst tur maçı oynandığı için kazanan değiştirilemez. |  |
| `TOURNAMENT_WINNER_NOT_IN_FIXTURE` | Kazanan takım bu maçta oynamıyor. |  |
| `TOURNAMENT_WITHDRAW_AFTER_START` | Turnuva başladıktan sonra takım çekilemez. |  |
| `TOURNAMENT_WITHDRAW_FORBIDDEN` | Takımı çekmek için takımın üyesi ya da turnuvanın yöneticisi olmalısın. |  |

#### MEDIA_*

| Kod | Mesaj (tr) | Yer tutucular |
|---|---|---|
| `MEDIA_FILE_EMPTY` | Dosya boş. |  |
| `MEDIA_FILE_TOO_LARGE` | Dosya boyutu {0}MB'ı geçemez. | {0}: en fazla boyut (MB) |
| `MEDIA_FORMAT_NOT_ALLOWED` | Sadece JPG, JPEG veya PNG dosyaları yüklenebilir. |  |
| `MEDIA_INVALID_IMAGE` | Dosya geçerli bir resim değil. |  |
| `MEDIA_INVALID_KIND` | Geçersiz medya türü (kind: message). |  |
| `MEDIA_NOT_FOUND` | Fotoğraf bulunamadı. |  |
| `MEDIA_NO_FILE` | Fotoğraf seçilmedi. |  |
| `MEDIA_TOO_LARGE` | Fotoğraf en fazla {0} MB olabilir. | {0}: en fazla boyut (MB) |
| `MEDIA_TOO_MANY_PIXELS` | Fotoğraf çok büyük (en fazla {0} MP). | {0}: en fazla megapiksel |

#### VENUE_*

| Kod | Mesaj (tr) | Yer tutucular |
|---|---|---|
| `VENUE_ADDRESS_EMPTY` | Adres boş olamaz. |  |
| `VENUE_AMOUNT_MUST_BE_POSITIVE` | Tutar 0'dan büyük olmalıdır. |  |
| `VENUE_AMOUNT_TOO_LARGE` | Tutar en fazla {0:0} olabilir. | {0}: en fazla tutar |
| `VENUE_AMOUNT_TOO_MANY_DECIMALS` | Tutar en fazla 2 ondalık basamak içerebilir. |  |
| `VENUE_AND_OWNER_REQUIRED` | Tesis ve işletme seçilmelidir. |  |
| `VENUE_BLOCK_TOO_LONG` | Tek blok en fazla {0} gün olabilir. | {0}: gün |
| `VENUE_BOOKING_TOO_FAR_AHEAD` | Bu tesiste en fazla {0} gün sonrasına rezervasyon yapılabilir. | {0}: gün |
| `VENUE_BOOKING_TOO_SOON` | Bu tesiste rezervasyon en az {0} saat önceden yapılmalıdır. | {0}: saat |
| `VENUE_CALENDAR_RANGE_TOO_LONG` | Takvim aralığı en fazla {0} gün olabilir. | {0}: gün |
| `VENUE_CAMPAIGN_EMPTY` | Kampanya boş olamaz. |  |
| `VENUE_CAMPAIGN_TITLE_EMPTY` | Kampanya başlığı boş olamaz. |  |
| `VENUE_CANDIDATE_EMPTY` | Aday bilgileri boş olamaz. |  |
| `VENUE_CHOOSE_SLOT` | Lütfen bir saha ve saat seçin. |  |
| `VENUE_CLOSED_AT_TIME` | Tesis bu saatte kapalı. |  |
| `VENUE_DISCOUNT_OR_FIXED_PRICE` | İndirim yüzdesi ya da sabit fiyattan tam olarak biri girilmelidir. |  |
| `VENUE_DURATION_OUT_OF_RANGE` | Süre {0} ile {1} dakika arasında olmalıdır. | {0}: en az, {1}: en fazla (dakika) |
| `VENUE_END_DATE_BEFORE_START` | Bitiş tarihi başlangıçtan önce olamaz. |  |
| `VENUE_END_TIME_BEFORE_START` | Bitiş zamanı başlangıçtan sonra olmalıdır. |  |
| `VENUE_EXPENSE_INFO_EMPTY` | Gider bilgileri boş olamaz. |  |
| `VENUE_EXPENSE_MUST_BE_SELECTED` | Gider seçilmelidir. |  |
| `VENUE_FIELD_MUST_BE_ENTERED` | {0} girilmelidir. | {0}: alan adı |
| `VENUE_FIELD_REQUIRED` | {0} zorunludur. | {0}: alan adı |
| `VENUE_INFO_EMPTY` | Tesis bilgileri boş olamaz. |  |
| `VENUE_INFO_REQUIRED` | Tesis bilgileri gerekli. |  |
| `VENUE_INVALID_BLOCK_KIND` | Geçersiz tür (0 = kapalı/bakım, 1 = harici rezervasyon). |  |
| `VENUE_INVALID_DATE` | Tarih geçersiz. |  |
| `VENUE_INVALID_DAYS_MASK` | En az bir gün seçilmelidir (daysMask 1–127). |  |
| `VENUE_INVALID_DURATION` | Maç süresi {0} dakika olmalıdır. | {0}: izin verilen süreler (ör. "60, 90 veya 120") |
| `VENUE_INVALID_EXPENSE_CATEGORY` | Kategori şunlardan biri olmalıdır: {0}. | {0}: virgülle ayrılmış kategori listesi |
| `VENUE_INVALID_HOUR_RANGE` | Saat aralığı geçersiz (fromHour 0–23, toHour 1–24, fromHour < toHour). |  |
| `VENUE_INVALID_LOCATION` | Geçersiz konum. |  |
| `VENUE_INVALID_LOCATION_FORMAT` | Konum "enlem, boylam" biçiminde olmalıdır (ör. 41.0, 29.0). |  |
| `VENUE_INVALID_START_MINUTE` | Başlangıç saati {0} dakikalık aralıklarla seçilmelidir (ör. :00, :30). | {0}: aralık (dakika) |
| `VENUE_INVALID_SURFACE` | Zemin 1 (suni çim), 2 (doğal çim), 3 (parke) veya 4 (diğer) olmalıdır. |  |
| `VENUE_INVALID_YEAR` | Yıl geçersiz. |  |
| `VENUE_LOCATION_REQUIRED` | Konum zorunludur ("enlem, boylam" biçiminde, ör. 41.0, 29.0). |  |
| `VENUE_MATCH_AND_TEAM_REQUIRED` | Maç ve takım seçilmelidir. |  |
| `VENUE_MEMBERSHIP_INACTIVE` | Üyeliğiniz aktif değil. |  |
| `VENUE_MUST_BE_SELECTED` | Tesis seçilmelidir. |  |
| `VENUE_NOT_FOUND` | Tesis bulunamadı. |  |
| `VENUE_NOT_YOURS` | Bu tesis size ait değil. |  |
| `VENUE_NO_CAMERA` | Bu tesiste maç kaydı (kamera) hizmeti yok. |  |
| `VENUE_NO_CLEAT_RENTAL` | Bu tesiste krampon kiralama yok. |  |
| `VENUE_NUMBER_OR_STRING_EXPECTED` | Metin ya da sayı bekleniyordu. |  |
| `VENUE_OPENING_HOURS_INVALID_DAYS` | dayOfWeek 0 (Pazartesi) … 6 (Pazar) arasında ve her gün bir kez olmalıdır. |  |
| `VENUE_OPENING_HOURS_INVALID_TIME` | Açılış ve kapanış saatleri "HH:mm" biçiminde olmalıdır. |  |
| `VENUE_OPENING_HOURS_SEVEN_DAYS` | 7 günün açılış saatleri gönderilmelidir. |  |
| `VENUE_OWNER_INFO_REQUIRED` | İşletme hesabı bilgileri gerekli. |  |
| `VENUE_OWNER_MUST_BE_SELECTED` | İşletme seçilmelidir. |  |
| `VENUE_OWNER_NOT_FOUND` | İşletme bulunamadı. |  |
| `VENUE_PAST_SLOT` | Geçmiş bir saat seçilemez. |  |
| `VENUE_PHOTO_LIMIT_EXCEEDED` | En fazla {0} fotoğraf yüklenebilir. | {0}: en fazla fotoğraf |
| `VENUE_PHOTO_REQUIRED` | En az {0} fotoğraf zorunludur. | {0}: en az fotoğraf |
| `VENUE_PITCH_INFO_EMPTY` | Saha bilgileri boş olamaz. |  |
| `VENUE_PITCH_MUST_BE_SELECTED` | Saha seçilmelidir. |  |
| `VENUE_PITCH_NAME_EMPTY` | Saha adı boş olamaz. |  |
| `VENUE_PITCH_NOT_FOUND` | Saha bulunamadı. |  |
| `VENUE_PITCH_REQUIRED` | Lütfen bir saha seçin. |  |
| `VENUE_PRICE_OUT_OF_RANGE` | {0} 0'dan büyük ve en fazla {1:0} olmalıdır. | {0}: alan adı, {1}: en fazla |
| `VENUE_PRICE_RULE_EMPTY` | Fiyat kuralı boş olamaz. |  |
| `VENUE_REPLY_EMPTY` | Yanıt boş olamaz. |  |
| `VENUE_RESERVATION_REQUIRED` | Rezervasyon seçilmelidir. |  |
| `VENUE_REVIEW_EMPTY` | Değerlendirme boş olamaz. |  |
| `VENUE_SETTINGS_OUT_OF_RANGE` | {0} {1} ile {2} arasında olmalıdır. | {0}: alan adı, {1}: en az, {2}: en fazla |
| `VENUE_SLOT_TAKEN` | Bu saat dolu. |  |
| `VENUE_START_END_DATE_REQUIRED` | Başlangıç ve bitiş tarihi gerekli. |  |
| `VENUE_START_END_TIME_REQUIRED` | Başlangıç ve bitiş zamanı gerekli. |  |
| `VENUE_START_TIME_REQUIRED` | Başlangıç zamanı gerekli. |  |
| `VENUE_USERNAME_TAKEN` | Bu kullanıcı adı zaten kullanılıyor. |  |

#### RESERVATION_*

| Kod | Mesaj (tr) | Yer tutucular |
|---|---|---|
| `RESERVATION_AWAY_TEAM_NOT_FOUND` | Rakip takım bulunamadı. |  |
| `RESERVATION_CLEATS_EXCEED_PLAYERS` | Krampon sayısı oyuncu sayısından fazla olamaz. |  |
| `RESERVATION_HOME_TEAM_CANNOT_BE_OPPONENT` | Ev sahibi takım rakip olarak katılamaz. |  |
| `RESERVATION_HOME_TEAM_NOT_FOUND` | Ev sahibi takım bulunamadı. |  |
| `RESERVATION_INVALID_STATUS` | Geçersiz durum. |  |
| `RESERVATION_MATCH_PAST_CLOSED` | Maç tarihi geçti, katılım kapalı. |  |
| `RESERVATION_MATCH_REJECTED_CLOSED` | Bu maç reddedildi, katılım kapalı. |  |
| `RESERVATION_NOT_HOME_TEAM_MEMBER` | Ev sahibi takımın üyesi olmalısın. |  |
| `RESERVATION_NOT_LOOKING_FOR_OPPONENT` | Bu maç rakip takım aramıyor. |  |
| `RESERVATION_NOT_PENDING` | Sadece onay bekleyen rezervasyonlar yanıtlanabilir. |  |
| `RESERVATION_NOT_TEAM_MEMBER` | Bu takımın üyesi değilsin. |  |
| `RESERVATION_OPPONENT_FLAG_CONFLICT` | Rakip takım seçiliyken 'rakip aranıyor' işaretlenemez. |  |
| `RESERVATION_OWNER_CANNOT_CREATE` | İşletme hesapları maç kuramaz. |  |
| `RESERVATION_PITCH_UNAVAILABLE` | Bu saha şu anda kullanılamıyor. |  |
| `RESERVATION_REVIEW_MATCH_NOT_PLAYED` | Maç henüz oynanmadı, yorum yapılamaz. |  |
| `RESERVATION_REVIEW_MATCH_REJECTED` | Reddedilen maç için yorum yapılamaz. |  |
| `RESERVATION_REVIEW_NOT_PARTICIPANT` | Sadece maçta oynayan oyuncular tesisi değerlendirebilir. |  |
| `RESERVATION_REVIEW_NOT_VENUE_MATCH` | Bu maç anlaşmalı bir tesiste oynanmadı. |  |
| `RESERVATION_REVIEW_SAVE_FAILED` | Değerlendirme kaydedilemedi, lütfen tekrar deneyin. |  |
| `RESERVATION_SAME_TEAMS` | Ev sahibi ve rakip takım aynı olamaz. |  |
| `RESERVATION_VENUE_CLOSED` | Bu tesis şu anda rezervasyona kapalı. |  |

#### OWNER_*

| Kod | Mesaj (tr) | Yer tutucular |
|---|---|---|
| `OWNER_BLOCK_CLOSED_CONFLICT` | Bu aralık kapalı (bakım/blok) olarak işaretli. |  |
| `OWNER_BLOCK_HAS_MANUAL` | Bu aralıkta bir harici rezervasyon var. |  |
| `OWNER_BLOCK_MANUAL_CONFLICT` | Bu aralıkta başka bir harici rezervasyon var. |  |
| `OWNER_BLOCK_NOT_FOUND` | Blok bulunamadı. |  |
| `OWNER_BLOCK_PAYMENT_ONLY_MANUAL` | Ödeme durumu sadece harici rezervasyonlar için değiştirilebilir. |  |
| `OWNER_BLOCK_RESERVATION_CONFLICT` | Bu aralıkta bekleyen ya da onaylı bir rezervasyon var. |  |
| `OWNER_CAMPAIGN_NOT_FOUND` | Kampanya bulunamadı. |  |
| `OWNER_EXPENSE_NOT_FOUND` | Gider bulunamadı. |  |
| `OWNER_LAST_PHOTO_CANNOT_DELETE` | Tesisin son fotoğrafı silinemez. |  |
| `OWNER_PHOTO_LIMIT_REACHED` | Bir tesise en fazla {0} fotoğraf eklenebilir. | {0}: en fazla fotoğraf sayısı |
| `OWNER_PHOTO_NOT_FOUND` | Fotoğraf bulunamadı. |  |
| `OWNER_PHOTO_REQUIRED` | Fotoğraf seçilmedi. |  |
| `OWNER_PITCH_HAS_RESERVATIONS` | Bu sahada gelecekte bekleyen ya da onaylı rezervasyon var. Sahayı silmek yerine pasife alabilirsiniz. |  |
| `OWNER_PITCH_NOT_IN_VENUE` | Saha bu tesise ait değil. |  |
| `OWNER_PRICE_RULE_NOT_FOUND` | Fiyat kuralı bulunamadı. |  |
| `OWNER_REVIEW_NOT_FOUND` | Yorum bulunamadı. |  |

#### ADMIN_*

| Kod | Mesaj (tr) | Yer tutucular |
|---|---|---|
| `ADMIN_CREATE_OWNER_FAILED` | İşletme oluşturulamadı (kullanıcı adı kullanımda olabilir). |  |
| `ADMIN_INVALID_OWNER_MODEL` | Geçersiz işletme bilgisi (model JSON). |  |
| `ADMIN_NOT_AN_OWNER` | Seçilen hesap bir işletme değil. |  |
| `ADMIN_OWNER_NOT_FOUND` | İşletme bulunamadı. |  |
| `ADMIN_PHOTO_UPLOAD_FAILED` | {0}: {1} | {0}: dosya adı, {1}: yükleme hata mesajı |
| `ADMIN_USERNAME_TAKEN` | Bu kullanıcı adı zaten kullanılıyor. |  |

#### OSM_*

| Kod | Mesaj (tr) | Yer tutucular |
|---|---|---|
| `OSM_ALL_MIRRORS_FAILED` | OpenStreetMap sunucularının hiçbiri yanıt vermedi. Biraz sonra tekrar deneyin ya da daha küçük bir alan seçin. |  |
| `OSM_ALREADY_IMPORTED` | Bu OpenStreetMap kaydı zaten içe aktarılmış. |  |
| `OSM_AREA_REQUIRED` | city ya da south/west/north/east verilmelidir. |  |
| `OSM_CITY_NOT_FOUND` | Şehir bulunamadı. |  |
| `OSM_INVALID_BOUNDS` | Geçersiz alan (en fazla {0}° x {0}°, south < north, west < east). | {0}: en büyük derece |
| `OSM_INVALID_RESPONSE` | Harita servisinden geçersiz yanıt alındı. |  |
| `OSM_SERVICE_ERROR` | Harita servisi hata döndü ({0}). | {0}: HTTP durum kodu |
| `OSM_UNREACHABLE` | Harita servisine ulaşılamadı. Lütfen daha sonra tekrar deneyin. |  |

<!-- toplam 278 kod -->


---

## 4. Sistem parametreleri

Business limits are no longer hard-coded. They live in the `SystemParameters` table and are read with `IParameterService`, which offers typed getters backed by `IMemoryCache`.

How the cache behaves:
- The whole table is held in one cache entry for 5 minutes.
- `PUT` clears that entry immediately, so the new value applies from the next request.
- Rate limits (`rateLimit.*`) also apply immediately, because the limit value is part of the limiter partition key.

Code keeps the definitions in `MatchBuddy.Core/Parameters/ParameterCatalog.cs` (key, type, default, group, min/max, public). At startup:
- Keys that are missing from the table are inserted with their default value.
- Metadata of existing rows (type, group, min/max, isPublic, description) is updated from code.
- **Existing values are never changed.**

If the database can't be reached, or a stored value is corrupt, the code default is used.

Settings that are security-critical stay in configuration, not in this table: the JWT key and issuer/audience, CORS origins, the connection string and storage keys.

### Tablolar

| Tablo | Alanlar |
|---|---|
| `SystemParameters` | `SystemParameterId` PK, `Key` nvarchar(100) **unique**, `Value` nvarchar(4000) (always text, invariant culture: `10`, `0.5`, `true`, `[60,90]`), `DataType` tinyint (1 int, 2 decimal, 3 bool, 4 string, 5 json), `Group` nvarchar(50), `Description` nvarchar(500) (Turkish, from resx `PARAMDESC_*`), `MinValue`/`MaxValue` nvarchar(50) null, `IsPublic` bit, `UpdatedAt` datetime2, `UpdatedByPlayerId` int null FK Players (NoAction) |
| `ParameterAudits` | `ParameterAuditId` bigint PK, `SystemParameterId` FK (Cascade), `Key`, `OldValue` null, `NewValue`, `ChangedByPlayerId` null FK Players, `ChangedAt`. Index (SystemParameterId, ChangedAt) |
| `VenueSettings` | `StadiumId` PK+FK Stadiums (Cascade), `MinBookingNoticeHours` int null, `FreeCancellationHours` int null, `MaxAdvanceBookingDays` int null, `UpdatedAt`, `UpdatedByPlayerId` null. **null = system default** |

### Parametre listesi

"Public = evet" means the parameter is returned by `GET /api/Parameters/Public`.

| Anahtar | Tür | Varsayılan | Min | Max | Grup | Public | Açıklama |
|---|---|---|---|---|---|---|---|
| `media.maxUploadMb` | int | `10` | 1 | 20 | Media | evet | Yüklenebilecek bir fotoğrafın en büyük boyutu (MB). |
| `media.maxMegapixels` | int | `40` | 1 | 100 | Media | evet | Yüklenebilecek bir fotoğrafın en fazla piksel sayısı (megapiksel). |
| `media.pendingCleanupHours` | int | `24` | 1 | 720 | Media | hayır | Hiçbir kayda bağlanmayan (bekleyen) medyanın silinmeden önce bekleyeceği süre (saat). |
| `media.deletedPurgeDays` | int | `7` | 1 | 365 | Media | hayır | Silinmiş medya satırlarının veritabanından tamamen kaldırılacağı süre (gün). |
| `media.cleanupBatchSize` | int | `500` | 10 | 10000 | Media | hayır | Medya temizliğinin bir turda işleyeceği en fazla kayıt sayısı. |
| `rateLimit.authPerMinute` | int | `10` | 1 | 1000 | RateLimit | hayır | Giriş/kayıt uç noktaları için IP başına dakikadaki en fazla istek. |
| `rateLimit.uploadPerMinute` | int | `20` | 1 | 1000 | RateLimit | hayır | Fotoğraf yükleme için kullanıcı başına dakikadaki en fazla istek. |
| `venue.maxPhotos` | int | `20` | 1 | 100 | Venue | evet | Bir tesise eklenebilecek en fazla fotoğraf sayısı. |
| `venue.maxPrice` | decimal | `100000` | 1 | 10000000 | Venue | evet | Saatlik ücret, kampanya, blok ve ekstra ücretleri için izin verilen en yüksek tutar (TL). |
| `venue.allowedDurations` | json | `[60,90,120]` |  |  | Venue | evet | Rezervasyonda seçilebilecek maç süreleri (dakika, JSON dizi). |
| `venue.slotStepMinutes` | int | `30` | 5 | 60 | Venue | evet | Rezervasyon başlangıç saatlerinin dakika aralığı (ör. 30 → :00 ve :30). |
| `venue.playersPerSideMin` | int | `5` | 1 | 11 | Venue | evet | Bir sahada takım başına en az oyuncu sayısı. |
| `venue.playersPerSideMax` | int | `11` | 1 | 15 | Venue | evet | Bir sahada takım başına en fazla oyuncu sayısı. |
| `venue.extraPlayersAllowed` | int | `4` | 0 | 20 | Venue | evet | Rezervasyonda saha kapasitesinin (takım başı × 2) üzerine eklenebilecek oyuncu sayısı. |
| `venue.bookingMinPlayersPerSide` | int | `4` | 1 | 11 | Venue | evet | Rezervasyonda takım başına en az oyuncu sayısı (toplam en az × 2). |
| `venue.calendarMaxDays` | int | `62` | 7 | 366 | Venue | hayır | İşletme takviminde sorgulanabilecek en uzun aralık (gün). |
| `venue.blockMaxDays` | int | `31` | 1 | 366 | Venue | hayır | Tek bir saat bloğunun en uzun süresi ve blok listesinin varsayılan aralığı (gün). |
| `venue.maxDiscountPercent` | int | `90` | 1 | 100 | Venue | hayır | Kampanyalarda izin verilen en yüksek indirim yüzdesi. |
| `venue.reviewsShown` | int | `20` | 1 | 200 | Venue | hayır | Tesis detayında gösterilen son değerlendirme sayısı. |
| `venue.createOwnerMaxPhotos` | int | `10` | 1 | 20 | Venue | hayır | Yönetici işletme oluştururken yüklenebilecek en fazla fotoğraf sayısı. |
| `venue.defaultMembershipDays` | int | `0` | 0 | 3650 | Venue | hayır | Yeni işletme üyeliğinin varsayılan süresi (gün; 0 = süresiz). Bitiş tarihi verilmezse kullanılır. |
| `booking.defaultMinNoticeHours` | int | `0` | 0 | 168 | Booking | evet | Rezervasyonun maç saatinden en az kaç saat önce yapılması gerektiği (tesis ayarı yoksa). |
| `booking.defaultFreeCancellationHours` | int | `24` | 0 | 720 | Booking | evet | Maçtan kaç saat öncesine kadar ücretsiz iptal yapılabileceği (tesis ayarı yoksa; bilgilendirme amaçlı). |
| `booking.defaultMaxAdvanceDays` | int | `90` | 1 | 365 | Booking | evet | En fazla kaç gün sonrasına rezervasyon yapılabileceği (tesis ayarı yoksa). |
| `match.reminderLeadHours` | int | `24` | 1 | 168 | Match | hayır | Maç hatırlatmasının maçtan kaç saat önce gönderileceği. |
| `match.maxScore` | int | `50` | 1 | 999 | Match | evet | Maç sonucunda bir takımın en fazla gol sayısı. |
| `match.maxPlayerStat` | int | `30` | 1 | 999 | Match | evet | Bir oyuncunun bir maçtaki en fazla gol/asist sayısı. |
| `match.ratingMax` | int | `5` | 2 | 10 | Match | evet | Oyuncu puanlamasında en yüksek puan (en düşük 1). |
| `match.leaderboardMax` | int | `100` | 1 | 1000 | Match | hayır | Liderlik tablosunda istenebilecek en fazla satır. |
| `social.maxInviteBatch` | int | `50` | 1 | 500 | Social | hayır | Tek seferde davet edilebilecek en fazla oyuncu sayısı. |
| `social.feedMaxTake` | int | `100` | 1 | 500 | Social | hayır | Akış ve keşfet listelerinde tek istekte dönebilecek en fazla kayıt. |
| `tournament.minTeams` | int | `4` | 2 | 64 | Tournament | evet | Turnuva oluştururken seçilebilecek en küçük takım kapasitesi. |
| `tournament.maxTeams` | int | `16` | 2 | 128 | Tournament | evet | Turnuva oluştururken seçilebilecek en büyük takım kapasitesi. |
| `tournament.leagueMinTeamsToStart` | int | `3` | 2 | 64 | Tournament | hayır | Lig usulü turnuvanın başlatılması için gereken en az takım. |
| `tournament.knockoutMinTeamsToStart` | int | `2` | 2 | 64 | Tournament | hayır | Eleme usulü turnuvanın başlatılması için gereken en az takım. |
| `tournament.roundIntervalDays` | int | `7` | 1 | 60 | Tournament | hayır | Turnuva turları arasındaki gün sayısı. |
| `tournament.pointsWin` | int | `3` | 1 | 10 | Tournament | hayır | Lig puan durumunda galibiyet puanı. |
| `tournament.pointsDraw` | int | `1` | 0 | 10 | Tournament | hayır | Lig puan durumunda beraberlik puanı. |
| `tournament.maxScore` | int | `50` | 1 | 999 | Tournament | hayır | Turnuva maçı sonucunda bir takımın en fazla gol sayısı. |
| `osm.maxResults` | int | `200` | 1 | 1000 | Osm | hayır | OpenStreetMap saha aramasında dönecek en fazla aday. |
| `osm.duplicateRadiusMeters` | int | `150` | 10 | 5000 | Osm | hayır | Aday sahanın mevcut bir tesise "zaten var" sayılacağı uzaklık (metre). |
| `osm.maxBoxDegrees` | decimal | `2` | 0.1 | 5 | Osm | hayır | OpenStreetMap aramasında alanın en büyük kenarı (derece). |
| `osm.overpassTimeoutSeconds` | int | `25` | 5 | 180 | Osm | hayır | Overpass sorgusu için sunucu tarafı zaman aşımı (saniye). |
| `security.passwordMinLength` | int | `6` | 6 | 64 | Security | evet | Oyuncu ve işletme şifrelerinin en az uzunluğu. |
| `security.tempPasswordLength` | int | `12` | 8 | 64 | Security | hayır | Yöneticinin ürettiği geçici işletme şifresinin uzunluğu. |

<!-- 45 parametre -->


Validation rules:
- `int` / `decimal` must parse with the invariant culture (`.` as the decimal separator) and lie within `minValue`–`maxValue`.
- `bool` must be `true` or `false`.
- `json` must be valid JSON. Parameters whose default is an array (`venue.allowedDurations`) must be **a non-empty array of positive integers**.

There are no cross-parameter checks, so the admin must keep pairs consistent, e.g. `venue.playersPerSideMin ≤ venue.playersPerSideMax` and `tournament.minTeams ≤ tournament.maxTeams`.

### Taşınan sabitler (eski yer → parametre)

| Eski sabit (dosya) | Parametre |
|---|---|
| `ImageProcessor.MaxBytes` 10 MB, `MaxPixels` 40 MP (Api/Services/Media) | `media.maxUploadMb`, `media.maxMegapixels` |
| `MediaCleanupService.PendingMaxAge` 24 sa | `media.pendingCleanupHours` |
| `MediaService.CleanupPendingAsync` `Take(500)`, `AddDays(-7)` | `media.cleanupBatchSize`, `media.deletedPurgeDays` |
| `RateLimiting:AuthPermitLimit` 10 / `RateLimiting:UploadPermitLimit` 20 (appsettings; **removed from appsettings**) | `rateLimit.authPerMinute`, `rateLimit.uploadPerMinute` |
| `EFVenueDal.Owner.MaxPhotosPerVenue` 20 | `venue.maxPhotos` |
| `VenueManager.MaxPrice` 100000 | `venue.maxPrice` |
| `VenuePricing.AllowedDurations` {60,90,120} | `venue.allowedDurations` |
| `VenueRules.CheckSlot` start minute :00/:30 | `venue.slotStepMinutes` (start time, in minutes since midnight, must be a multiple of the step) |
| `VenueManager.SavePitch` players per side 5–11 | `venue.playersPerSideMin`, `venue.playersPerSideMax` |
| `VenuePricing.MinPlayersPerSide` 4, `ExtraPlayers` 4 (booking userCount range) | `venue.bookingMinPlayersPerSide`, `venue.extraPlayersAllowed` |
| `VenueManager.Calendar` 62 days | `venue.calendarMaxDays` |
| `VenueManager` block 31 days (max length + default list range) | `venue.blockMaxDays` |
| `VenueManager` campaign discount 1–90 % | `venue.maxDiscountPercent` |
| `EFVenueDal.Detail` last 20 reviews | `venue.reviewsShown` |
| `VenueManager.ValidateCreateOwner` max 10 photos | `venue.createOwnerMaxPhotos` |
| (new) default membership length on CreateOwner when `membershipUntil` is empty | `venue.defaultMembershipDays` (0 = unlimited, as before) |
| (new) venue settings defaults | `booking.defaultMinNoticeHours`, `booking.defaultFreeCancellationHours`, `booking.defaultMaxAdvanceDays` |
| `EFMatchParticipationDal.SendDueReminders` `AddHours(24)` | `match.reminderLeadHours` |
| `MatchParticipationManager.MaxScore` 50 / `MaxPlayerStat` 30 | `match.maxScore`, `match.maxPlayerStat` |
| `MatchParticipationManager.RatePlayers` 1–5 (+ `UserScore = avg × 20`) | `match.ratingMax` (`UserScore = round(avg × 100 / ratingMax)`, still 0–100) |
| `MatchParticipationManager.GetLeaderboard` clamp 100 | `match.leaderboardMax` |
| `InviteManager.MaxInviteBatch` 50 | `social.maxInviteBatch` |
| `SocialManager` Discover/Feed `take` clamp 100 | `social.feedMaxTake` |
| `TournamentManager.MinTeams` 4 / `MaxTeams` 16 | `tournament.minTeams`, `tournament.maxTeams` |
| `EFTournamentDal.Start` league ≥3 / knockout ≥2 teams | `tournament.leagueMinTeamsToStart`, `tournament.knockoutMinTeamsToStart` |
| `TournamentScheduler.RoundDate` 7 days; standings 3/1 points; `MaxScore` 50 | `tournament.roundIntervalDays`, `tournament.pointsWin`, `tournament.pointsDraw`, `tournament.maxScore` |
| `OsmService` `out … 200`, `timeout:25`, 150 m; `AdminController` box 2°×2° | `osm.maxResults`, `osm.overpassTimeoutSeconds`, `osm.duplicateRadiusMeters`, `osm.maxBoxDegrees` |
| `PlayerManager.MinPasswordLength` 6 (registration/profile) + `VenueManager.ValidateCreateOwner` owner password 6 | `security.passwordMinLength` |
| `VenueManager.GenerateTemporaryPassword` length 12 | `security.tempPasswordLength` |

Deliberately **left in code**, because they are structural rules, data formats or field lengths rather than business limits:
- string max lengths (DB column sizes) and minimum name lengths
- the 1–5 **venue** review rating
- the score lower bound (0)
- days mask 1–127, pitch surface 1–4, the 0–2 privacy choices
- image variant sizes and WebP quality
- default opening hours 09:00–00:00
- the 2-hour busy-slot look-back
- finance/report windows (12 months, year 2000–2100)
- feed/preview sizes (5 players, 3 followings, 5 recent matches, 6 members)
- `Reminders:IntervalSeconds` and `Media:CleanupIntervalMinutes` (ops config)
- the 22 MB request-size ceiling on upload endpoints, which is the maximum `media.maxUploadMb` plus form overhead; the actual file limit is the parameter

### Admin uç noktaları (rol: Admin)

All of them return the standard error body. A player gets 403 `AUTH_FORBIDDEN` and an anonymous caller gets 401 `AUTH_UNAUTHORIZED`.

#### `GET /api/Admin/Parameters`
Returns all parameters, grouped. Groups follow catalog order; parameters inside a group follow catalog order.
```json
[
  { "group": "Media", "parameters": [
    { "key": "media.maxUploadMb", "value": "10", "dataType": "int", "group": "Media",
      "description": "Yüklenebilecek bir fotoğrafın en büyük boyutu (MB).",
      "minValue": "1", "maxValue": "20", "defaultValue": "10", "isPublic": true,
      "updatedAt": "2026-11-06T22:30:00", "updatedByPlayerId": null, "updatedByUserName": null }
  ]},
  { "group": "RateLimit", "parameters": [ … ] }
]
```
- `value`, `minValue`, `maxValue` and `defaultValue` are **always strings**.
- `dataType` is one of `"int" | "decimal" | "bool" | "string" | "json"`.

#### `PUT /api/Admin/Parameters/{key}`
Body: `{ "value": "15" }`. The value is always sent as a string, e.g. `"true"` or `"[60,90]"`. The key is case-insensitive.

On success the value is normalized (e.g. `" 15 "` → `"15"`, JSON is re-serialized) and saved. An audit row is written only if the value changed, and the cache is cleared.
```json
200 { "success": true, "message": "Parametre güncellendi.", "parameter": { …ParameterDto… } }
```

| Hata | HTTP | `errors` |
|---|---|---|
| `PARAM_NOT_FOUND` | 404 | — |
| `PARAM_VALUE_REQUIRED` (missing/empty `value`) | 400 | `value` |
| `PARAM_INVALID_TYPE` | 400 | `value` |
| `PARAM_OUT_OF_RANGE` | 400 | `value` |
| `PARAM_INVALID_JSON` / `PARAM_INVALID_LIST` | 400 | `value` |

#### `GET /api/Admin/Parameters/{key}/History?take=50`
Returns the change history, newest first. `take` is clamped to 1–500. An unknown key returns 404 `PARAM_NOT_FOUND`.
```json
[ { "id": 12, "key": "venue.maxPhotos", "oldValue": "20", "newValue": "15",
    "changedByPlayerId": 7, "changedByUserName": "admin", "changedAt": "2026-11-06T22:31:05" } ]
```

### Herkese açık uç nokta

#### `GET /api/Parameters/Public` (anonim, `Cache-Control: public, max-age=300`)
Returns a flat object of **typed** values for the `IsPublic` parameters only:
```json
{
  "media.maxUploadMb": 10, "media.maxMegapixels": 40,
  "venue.maxPhotos": 20, "venue.maxPrice": 100000, "venue.allowedDurations": [60, 90, 120],
  "venue.slotStepMinutes": 30, "venue.playersPerSideMin": 5, "venue.playersPerSideMax": 11,
  "venue.extraPlayersAllowed": 4, "venue.bookingMinPlayersPerSide": 4,
  "booking.defaultMinNoticeHours": 0, "booking.defaultFreeCancellationHours": 24, "booking.defaultMaxAdvanceDays": 90,
  "match.maxScore": 50, "match.maxPlayerStat": 30, "match.ratingMax": 5,
  "tournament.minTeams": 4, "tournament.maxTeams": 16,
  "security.passwordMinLength": 6
}
```
Suggested frontend use:
- Upload size checks and the photo counter (`media.maxUploadMb`, `venue.maxPhotos`).
- Duration dropdown (`venue.allowedDurations`) and start-time step (`venue.slotStepMinutes`).
- Pitch form player range.
- Booking `userCount` range: `2 × bookingMinPlayersPerSide` to `2 × pitch.playersPerSide + extraPlayersAllowed`.
- Result/rating inputs, tournament capacity, password length.

The server always re-validates. Because of the 5-minute HTTP cache, a client may see a changed value up to 5 minutes late.

---

## 5. Tesis (işletme) ayarları — VenueSettings

These are per-venue rules. A `null` field means "use the system default" (the `booking.*` parameters).

| Alan | Aralık | Varsayılan parametre | Uygulama |
|---|---|---|---|
| `minBookingNoticeHours` | 0–168 | `booking.defaultMinNoticeHours` (0) | A booking must start at least N hours from now |
| `freeCancellationHours` | 0–720 | `booking.defaultFreeCancellationHours` (24) | **Informational**: stored and returned. Show it to players next to `cancellationPolicy`. There is no reservation-cancellation flow to enforce yet |
| `maxAdvanceBookingDays` | 1–365 | `booking.defaultMaxAdvanceDays` (90) | A booking's date can be at most N days after today |

Enforcement happens in the player booking flow (`VenueRules.CheckSlot`):
- `POST /api/Venues/Quote` returns `200 { available: false, reason: "<mesaj>" }`.
- `POST /api/Match/SaveMatch` (venue reservation) returns **400** with one of:
  - `VENUE_BOOKING_TOO_SOON` — "Bu tesiste rezervasyon en az {0} saat önceden yapılmalıdır."
  - `VENUE_BOOKING_TOO_FAR_AHEAD` — "Bu tesiste en fazla {0} gün sonrasına rezervasyon yapılabilir."

The owner's own manual bookings (blocks / external reservations) are not restricted.

> Note: the 90-day default horizon is new. Before this change there was no upper limit on how far ahead a player could book.

### `GET /api/Owner/Settings?stadiumId=12` (rol: Owner, tesisin sahibi)
```json
{
  "stadiumId": 12,
  "minBookingNoticeHours": null, "freeCancellationHours": 12, "maxAdvanceBookingDays": null,
  "effective": { "minBookingNoticeHours": 0, "freeCancellationHours": 12, "maxAdvanceBookingDays": 90 },
  "defaults":  { "minBookingNoticeHours": 0, "freeCancellationHours": 24, "maxAdvanceBookingDays": 90 },
  "updatedAt": "2026-11-06T22:40:00"
}
```
`updatedAt` is null if the venue has never been saved.

Errors:
- 404 `VENUE_NOT_FOUND`
- 403 `VENUE_NOT_YOURS`
- 403 `AUTH_FORBIDDEN` (not an owner)

### `PUT /api/Owner/Settings?stadiumId=12`
Body: `{ "minBookingNoticeHours": 2, "freeCancellationHours": 12, "maxAdvanceBookingDays": 30 }`. `stadiumId` can also be sent in the body; the query string wins.

**This is a full replace:** any field that is missing or `null` goes back to the system default. To change one field, send the current values of the others.
```json
200 { "success": true, "message": "Kaydedildi.", "settings": { …GET şekli… } }
```

Errors:
- 400 `VENUE_SETTINGS_OUT_OF_RANGE` ("{alan} {min} ile {max} arasında olmalıdır.")
- 404 `VENUE_NOT_FOUND`
- 403 `VENUE_NOT_YOURS`
- 403 `VENUE_MEMBERSHIP_INACTIVE` (writing requires an active membership, as for other owner write endpoints)

---

## 6. Oyuncu tercihleri — PlayerSettings eklemeleri

Two new columns in the `PlayerSettings` table:
- `Theme` nvarchar(10), default `'system'`
- `Language` nvarchar(10), default `'tr'`

The existing endpoints in `docs/social-api.md` are extended:

| Yöntem | URL | Değişiklik |
|---|---|---|
| GET | `/api/Social/Settings` | Response gains `"theme": "system" \| "light" \| "dark"` and `"language": "tr"` |
| POST | `/api/Social/Settings` | Body gains optional `theme` and `language`. Missing fields keep their current value, as before. Values are trimmed and lower-cased |

Errors (400, with an `errors` field):
- `SOCIAL_INVALID_THEME` (`errors.theme`)
- `SOCIAL_INVALID_LANGUAGE` (`errors.language`)

Accepted languages are those in `Localization:SupportedCultures`, currently `tr` and `en`.

The server **does not** pick the request language from `language` on its own. The frontend should read it after login and send it as `Accept-Language` on every request (§2), and apply `theme` in the UI. `system` means following the OS `prefers-color-scheme`.

---

## 7. Platform yapılandırması

`appsettings.json` (environment variables use the `__` separator, e.g. `Proxy__KnownProxies__0`):
```json
{
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" },
               "File": { "Path": "logs/matchbuddy-.log", "RetainedFileCountLimit": 14 } },
  "Localization": { "DefaultCulture": "tr", "SupportedCultures": [ "tr", "en" ] },
  "Proxy": { "KnownProxies": [], "KnownNetworks": [], "ForwardLimit": 1 },
  "Swagger": { "Enabled": false },
  "Cors": { "AllowedOrigins": [ "http://localhost:3000" ] },
  "Jwt": { "Issuer": "MatchBuddy.Api", "Audience": "MatchBuddy.Client", "ExpiresMinutes": 120, "Key": "" }
}
```
`appsettings.Development.json` sets `"Swagger": { "Enabled": true }`.

### 7.1 Ters vekil (ForwardedHeaders)
- `X-Forwarded-For` and `X-Forwarded-Proto` are honoured **only** when they come from a trusted proxy. **Loopback (127.0.0.1, ::1) is always trusted.**
- Add others with:
  - `Proxy:KnownProxies`: IP list, e.g. `["10.0.0.5"]`
  - `Proxy:KnownNetworks`: CIDR list, e.g. `["10.0.0.0/8", "172.16.0.0/12"]`
- An invalid value stops the app at startup with an explanatory error.
- `Proxy:ForwardLimit` (default 1) is how many proxy hops to read from the right of `X-Forwarded-For`. `0` means no limit.
- **Effect:** `RemoteIpAddress`, which the rate limiter partitions on and the request logs record (`ip`), becomes the real client IP.
  - Behind a tunnel or reverse proxy on the same machine (cloudflared, nginx, IIS ARR), it works with no configuration.
  - With a proxy on another machine, add it to the lists above.
- Untrusted sources cannot spoof the IP with this header.

### 7.2 Swagger
`Swagger:Enabled` (default **true in Development, false otherwise**). When false, neither `/swagger` nor the JSON document is served. The package is Swashbuckle.AspNetCore 10 (OpenAPI 3.x), with the Bearer "Authorize" button.

### 7.3 Loglama (Serilog)
- **Sinks:**
  - Console.
  - A daily rolling file at `Logging:File:Path`, relative to the content root, default `logs/matchbuddy-YYYYMMDD.log`. `Logging:File:RetainedFileCountLimit` (14) is how many files to keep. The folder is in `.gitignore`.
- **Levels:** `Logging:LogLevel` / the `Serilog` section (`ReadFrom.Configuration`). `Microsoft.AspNetCore` and `Microsoft.EntityFrameworkCore` are kept at Warning.
- **Request log:** one line per request:
  `HTTP {RequestMethod} {RequestPath} → {StatusCode} in {Elapsed} ms (user {UserId}, ip {ClientIp}, trace {TraceId})`
  - 5xx and exceptions are logged at Error.
  - `/health` is logged at Verbose (hidden).
  - The file sink also writes the properties as JSON.

### 7.4 İstek sınırları (rate limiting)

| Politika | Bölümleme | Sınır (dakikada) | Uç noktalar |
|---|---|---|---|
| `auth` | Client IP | `rateLimit.authPerMinute` (10) | `PlayerLogin`, `SavePlayer` |
| `upload` | User id (otherwise IP) | `rateLimit.uploadPerMinute` (20) | `POST /api/Media/Upload`, `POST /api/Owner/Photos/Upload` |

On rejection: 429 `COMMON_RATE_LIMITED` with a `Retry-After` header in seconds. Other endpoints are not limited. `/health` is never limited.

### 7.5 Sağlık kontrolü
`GET /health`: anonymous, no auth or rate limit, checks the database connection (`CanConnect`).
```json
200 { "status": "Healthy", "totalDurationMs": 12.3, "checks": { "database": { "status": "Healthy", "durationMs": 10.1 } } }
503 { "status": "Unhealthy", … }
```

### 7.6 Kimlik doğrulama hataları
JWT `OnChallenge` and `OnForbidden` write the standard body:
- 401 `AUTH_UNAUTHORIZED` covers a missing, invalid or expired token alike. On this code the frontend should clear the session and redirect to the login page.
- 403 `AUTH_FORBIDDEN`.

### 7.7 İşlenmeyen hatalar
`IExceptionHandler` returns 500 `COMMON_INTERNAL_ERROR` with a `traceId`. The full exception goes to the log. The body's `detail` field (exception text) appears **only in Development**.

---

## 8. Derleme / çalışma

- **Target:** `net10.0` (LTS). `global.json` pins SDK `10.0.100` with `rollForward: latestFeature`, so any 10.0.x SDK is accepted.
- **Packages:**
  - EF Core 10.0.14 (SqlServer/Design/Tools)
  - JwtBearer 10.0.14
  - Swashbuckle.AspNetCore 10.3.1
  - Serilog.AspNetCore 10.0.0 + Serilog.Sinks.File 7.0.0
  - System.Security.Cryptography.Pkcs 10.0.14 (pin for a vulnerable transitive version)
  - Removed: Microsoft.AspNetCore.OpenApi (Swashbuckle is used), Autofac (unused), the old 2.2 `Microsoft.AspNetCore.*.Abstractions`
- **EF tool:** a local tool manifest in the repo root (`dotnet-tools.json`, dotnet-ef 10.0.14):
  ```
  dotnet tool restore
  dotnet tool run dotnet-ef database update --project src/MatchBuddy.DataAccess --startup-project src/MatchBuddy.DataAccess
  ```
- **Migration:** `20261106193535_PlatformParameters`. It is additive: `SystemParameters`, `ParameterAudits`, `VenueSettings`, and `PlayerSettings.Theme` / `PlayerSettings.Language`. Parameter rows are filled in at application startup.
- **Visual Studio 2022 cannot build .NET 10 projects.** Use one of:
  - Visual Studio 2026 (18.x)
  - VS Code + C# Dev Kit
  - `dotnet` CLI 10.0.x
