# Auth API: oturum, sosyal giriş, şifre sıfırlama, e-posta doğrulama, KVKK

Base URL: `/api/Auth`. Hata gövdesi ve kodlar için bkz. `platform-api.md` §1. Tüm metinler `Messages.resx` (`AUTH_*`, `MSG_AUTH_*`, `MAIL_*`, `SMS_*`).

## Oturum modeli

- **Access token** (JWT) yanıt gövdesinde döner, süresi `auth.accessTokenMinutes` (varsayılan 15 dk).
- **Refresh token** HttpOnly çerezde (`mb_rt`, yol `/api/Auth`). Frontend istekleri `credentials: 'include'` / `withCredentials: true` ile atmalı.
  - "Beni hatırla" açık → kalıcı çerez, `auth.refreshTokenDays` (30 gün). Kapalı → oturum çerezi, en fazla `auth.sessionRefreshTokenHours`.
  - Her `Refresh` çağrısı token'ı döndürür (rotation). Kullanılmış bir token tekrar gelirse tüm aile iptal edilir (`AUTH_REFRESH_REUSED`).
    Aynı anda gelen istekler için `auth.refreshReuseGraceSeconds` toleransı vardır.
- Eski `POST /api/Players/PlayerLogin` çalışmaya devam eder (access token `auth.legacyAccessTokenMinutes`, refresh çerezi de yazılır).

### Giriş yanıtı (`AuthLoginResponse`)

```json
{
  "token": "eyJ…", "expiresAt": "…", "player": { … }, "role": 0,
  "refreshExpiresAt": "…", "rememberMe": true,
  "consentRequired": false, "termsVersion": "2026-11-06",
  "emailConfirmed": true, "isNew": false,
  "profileIncomplete": true, "missingProfileFields": ["age"]
}
```

- `consentRequired: true` → güncel koşullar kabul edilmemiş; `AcceptTerms` ekranı gösterilir.
- `isNew: true` → sosyal girişle yeni hesap açıldı; `profileIncomplete` ise profil tamamlama önerilir.

## Uç noktalar

| Yöntem | Yol | Yetki | Gövde | Not |
|---|---|---|---|---|
| POST | `Login` | anonim | `{ userName, password, rememberMe }` | 401 `AUTH_INVALID_CREDENTIALS` |
| POST | `Refresh` | çerez | — | 401 `AUTH_REFRESH_INVALID` / `AUTH_REFRESH_REUSED` |
| POST | `Logout` | çerez | — | Bu cihaz |
| POST | `LogoutAll` | JWT | — | Tüm cihazlar |
| POST | `ChangePassword` | JWT | `{ currentPassword, newPassword }` | Diğer oturumlar kapanır, bu cihaza yeni oturum |
| GET | `Providers` | anonim | — | `{ google: { clientId } \| null, facebook: { appId } \| null }` — butonlar buna göre gösterilir |
| POST | `Google` | anonim | `{ idToken, rememberMe }` | Google Identity Services `credential` |
| POST | `Facebook` | anonim | `{ accessToken, rememberMe }` | Facebook JS SDK `authResponse.accessToken` |
| GET | `ExternalLogins` | JWT | — | `{ hasPassword, logins[] }` |
| DELETE | `ExternalLogins/{provider}` | JWT | — | Tek giriş yöntemi kaldırılamaz (`AUTH_EXTERNAL_LOGIN_LAST_METHOD`) |
| POST | `ForgotPassword` | anonim | `{ identifier, channel: "email" \| "sms" }` | Her zaman aynı 200 yanıtı (hesap listelenemez) |
| POST | `ResetPassword` | anonim | `{ identifier, code, newPassword }` | 6 haneli kod; tüm oturumlar kapanır |
| POST | `VerifyEmail` | JWT | `{ code }` | |
| POST | `ResendVerification` | JWT | — | 429 `AUTH_CODE_RESEND_COOLDOWN` + `Retry-After` |
| POST | `AcceptTerms` | JWT | `{ termsVersion, marketingConsent }` | |
| GET | `/api/Me/Export` | JWT | — | KVKK veri dışa aktarımı |

Kayıt (`POST /api/Players/PlayerRegister`) artık `termsVersion` (güncel `legal.termsVersion`) ve isteğe bağlı `marketingConsent` ister.

## Sosyal giriş eşleştirme

1. Sağlayıcı + sağlayıcı kullanıcı kimliği daha önce bağlandıysa → o hesap.
2. Sağlayıcı e-postayı doğrulanmış verdiyse ve bu e-postaya sahip, e-postası MatchBuddy'de de doğrulanmış **tek** bir hesap varsa → hesaba bağlanır.
   Facebook e-postası yalnızca `auth.facebookEmailTrusted = true` ise güvenilir sayılır.
3. Aksi halde yeni hesap açılır (`hasPassword = false`); şifre "Şifremi unuttum" ile belirlenebilir.

## Şifre sıfırlama

- `identifier`: kullanıcı adı, e-posta ya da telefon. Kod hesabın **kayıtlı** e-postasına ya da telefonuna gider.
- Kod süresi `auth.codeTtlMinutes`, en fazla `auth.codeMaxAttempts` yanlış deneme.
- Sınırlar: IP başına `auth.passwordResetPerMinute`/dk, hesap başına `auth.passwordResetPerIdentifierPerHour`/saat.
- Başarılı sıfırlama e-postayı (kanal e-postaysa) ya da telefonu (SMS ise) doğrulanmış işaretler.

## Yapılandırma (gizli değerler `appsettings` / user-secrets / ortam değişkeni)

| Anahtar | Açıklama |
|---|---|
| `Auth:Google:ClientId` | Google OAuth istemci kimliği (Web). Boşsa Google butonu gizlenir. |
| `Auth:Facebook:AppId`, `Auth:Facebook:AppSecret` | Facebook uygulaması. Boşsa Facebook butonu gizlenir. |
| `Auth:FrontendBaseUrl` | E-posta bağlantıları için frontend adresi. |
| `Auth:RefreshCookie:*` | Çerez adı, yolu, SameSite, Domain. Frontend ve API farklı sitedeyse `SameSite=None` (HTTPS şart). |
| `Email:Smtp:*` | SMTP. Boşsa e-postalar `App_Data/outbox/*.txt` dosyasına yazılır (geliştirme). |
| `Sms:Netgsm:*` | Netgsm SMS. Boşsa SMS'ler outbox'a yazılır. |
