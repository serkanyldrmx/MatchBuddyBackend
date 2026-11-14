# E-posta merkezi — API sözleşmesi

Şablonlar, toplu / düzenli gönderimler (yönetici ve işletme), hoş geldin e-postası, isteğe bağlı maç hatırlatma
e-postaları ve abonelikten çıkma. Gönderim `IOutgoingMessageQueue` üzerinden yapılır (SMTP yoksa outbox klasörü).
Zamanlayıcı: `EmailSchedulerService` dakikada bir `IEmailCenterService.RunDue()` çağırır.

## Şablonlar

Gövde düz metindir; boş satır paragraf ayırır, HTML yazılamaz (her şey kodlanır), uygulama görünümündeki yerleşime
(`EmailLayout`: koyu üst bant + neon yeşil düğme) konur. Kimlik e-postaları (`AuthTemplates`) da aynı kabuğu kullanır.

Biçimler: `# Başlık` (gövdenin ilk satırıysa üst bantta büyük başlık), `## Ara başlık`, `- madde`, `> vurgu kutusu`,
`| Etiket: değer` (ardışık satırlar bilgi kartı olur), `**kalın**`, `---` (ayraç).

Alanlar: `{{ad}} {{soyad}} {{kullanici}} {{site}} {{tesis}} {{mac}} {{tarih}} {{saat}} {{saha}} {{adres}} {{kalan}} {{link}}`
(`tesis`: işletme gönderimlerinde işletmenin ilk tesisinin adı).
Düğme bağlantısı `http(s)://…` ya da bir alanla (`{{link}}`, `{{site}}/home`) başlamalı.

Sistem şablonları (silinemez, metni değişir, kapatılabilir):

| Key | Ne zaman |
|---|---|
| `welcome` | Kayıt (`POST /api/Players/Register…`) ve sosyal girişle açılan yeni hesap, e-posta varsa |
| `match_reminder` | Oyuncunun maç için seçtiği hatırlatma zamanı geldiğinde (yalnızca "katılıyor" iken, reddedilmemiş maç) |

## Uç noktalar

`{area}` = `Admin` (rol Admin) ya da `Owner` (rol Owner; yalnızca kendi şablon / gönderimleri).

| Uç nokta | Açıklama |
|---|---|
| `GET /api/Email/{area}/Templates` | Şablonlar (yönetici listesine eksik sistem şablonları eklenir) |
| `GET /api/Email/{area}/Starters` | Hazır şablon galerisi (kodda, `EmailStarters`; yönetici 9, işletme 7): `{ key, category, name, subject, body, buttonText, buttonUrl }` |
| `POST /api/Email/{area}/Templates` | `{ emailTemplateId?, name, subject, body, buttonText?, buttonUrl?, isActive }` ekle / güncelle |
| `POST /api/Email/{area}/Templates/Delete?id=` | Sistem şablonu silinemez (`EMAIL_SYSTEM_TEMPLATE`) |
| `GET /api/Email/{area}/Campaigns` | Gönderimler (en yeni önce, 200) |
| `GET /api/Email/{area}/AudienceCount?audience=` | Kitledeki alıcı sayısı |
| `POST /api/Email/{area}/Campaigns` | `{ subject, body, buttonText?, buttonUrl?, audience, scheduledAt? (boş = hemen), repeatDays? (7 / 30) }` |
| `POST /api/Email/{area}/Campaigns/Cancel?id=` | Planlanmış gönderimi iptal et |
| `POST /api/Email/{area}/Preview` | `{ subject, body, buttonText?, buttonUrl? }` → `{ subject, html }` (örnek değerlerle) |
| `POST /api/Email/{area}/SendTest` | Aynı gövde; giriş yapanın e-postasına `[Test]` önekiyle gönderir |
| `GET /api/Email/MatchReminders?matchId=` | Oyuncu: `{ minutes: [60,180], options: [60,180,1440], hasEmail }` |
| `POST /api/Email/MatchReminders` | `{ matchId, minutes: [...] }` — boş liste kapatır; maçta (katılıyor / yedek) olmak ve e-posta gerekir |
| `GET /api/Email/Unsubscribe?p=&t=` | Herkese açık; imzalı (HMAC, Jwt:Key) bağlantı → `MarketingConsent = false`, basit HTML sayfa |

## Kitleler (`audience`)

| Değer | Kim | Kim kullanabilir |
|---|---|---|
| 0 | Pazarlama izni veren oyuncular (abonelikten çıkma bağlantılı) | Yönetici |
| 1 | İşletme hesapları | Yönetici |
| 2 | Tüm oyuncular — yalnızca hizmet duyurusu (reklam içermemeli) | Yönetici |
| 3 | Tesiste son 1 yılda "katılıyor" olmuş ve pazarlama izni veren oyuncular (abonelikten çıkma bağlantılı) | İşletme |

İşletmeler en fazla 7 günde bir gönderebilir (`EMAIL_OWNER_TOO_FREQUENT`). Gönderim parça parça yapılır
(turda 150 alıcı, `Status = 1` sürerken), bitince tekrar eden gönderim bir sonraki tarihe planlanır.

Oyuncu pazarlama iznini Ayarlar → Bildirimler → "Kampanya ve duyuru e-postaları" ile değiştirir
(`POST /api/Social/Settings { marketingConsent }`, `GET` yanıtında `marketingConsent`).

## Hata kodları

`EMAIL_TEMPLATE_NOT_FOUND`, `EMAIL_SYSTEM_TEMPLATE`, `EMAIL_CAMPAIGN_NOT_FOUND`, `EMAIL_CAMPAIGN_LOCKED`, `EMAIL_INVALID_URL`,
`EMAIL_INVALID_SCHEDULE`, `EMAIL_OWNER_TOO_FREQUENT`, `EMAIL_NO_ADDRESS`, `EMAIL_NOT_IN_MATCH`, alan doğrulamaları `VALIDATION_*`.
