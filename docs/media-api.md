# Medya (fotoğraf) altyapısı — sözleşme
> **Hata gövdeleri:** bu dokümandaki tüm hata yanıtları artık standart gövdeyi kullanır: `{ success:false, code, message, errors?, traceId }`. Hata kodları, yerelleştirme ve parametreler için bkz. [platform-api.md](platform-api.md). Mesaj örnekleri `message` alanının Türkçe metnidir.

Amaç: tüm fotoğraflar (profil, tesis, mesaj) tek bir boru hattından geçer:
doğrula → EXIF/GPS temizle + yönü düzelt → WebP'ye çevir → 3 boy üret → depoya yaz → `Media` tablosuna kaydet.
Depo sağlayıcıdan bağımsızdır: geliştirmede **yerel disk**, canlıda **Cloudflare R2** (S3 uyumlu; AWS S3 / başka S3 sunucusu da aynı kodla çalışır).

## 1. Depolama soyutlaması

```csharp
public interface IFileStorage
{
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);           // yoksa sessizce geçer
    Task DeletePrefixAsync(string prefix, CancellationToken ct);  // bir medyanın tüm varyantları
}
```

- `LocalFileStorage`: `Storage:Local:Root` (varsayılan `{ContentRoot}/App_Data/media`). API bu klasörü **`/media`** yolundan statik dosya olarak sunar.
  Yanıt başlığı: `Cache-Control: public, max-age=31536000, immutable` (anahtarlar değişmez, her yükleme yeni GUID).
- `S3FileStorage` (AWSSDK.S3): `Storage:S3:{ServiceUrl, Bucket, AccessKey, SecretKey, PublicBaseUrl, Region="auto"}`, `ForcePathStyle=true`.
  R2 ile uyum için checksum hesaplama "gerektiğinde" moduna alınır (`RequestChecksumCalculation = WHEN_REQUIRED` / `ResponseChecksumValidation = WHEN_REQUIRED`) veya bu ayarları gerektirmeyen bir SDK sürümü seçilir. Put'ta `Cache-Control` aynı (immutable).
  Sağlayıcı S3 iken API'deki `GET /media/{**key}` isteği `PublicBaseUrl/{key}` adresine **302** yönlendirir (frontend yeniden yapılandırılmasa da çalışsın).
- Seçim: `Storage:Provider` = `Local` (varsayılan) | `S3`. Gizli anahtarlar user-secrets / ortam değişkeninde; appsettings'e sadece boş şablon.

### Anahtar ve URL biçimi
- Anahtar: `{kind}/{yyyy}/{MM}/{32 hex guid}/{sm|md|lg}.webp` (kind: `avatars`, `venues`, `messages`).
- DB'de saklanan URL **sağlayıcıdan bağımsız göreli yol**: `/media/{kind}/{yyyy}/{MM}/{guid}/md.webp`.
  Diğer boylar dosya adı değiştirilerek elde edilir (`sm.webp`, `lg.webp`). Frontend `fileUrl()` bunu `API_BASE_URL` ile birleştirir
  (veya `REACT_APP_MEDIA_BASE_URL` tanımlıysa `/media/` önekini onunla değiştirir — R2 özel alan adı için).
- Eski `/uploads/...` URL'leri geçiş tamamlanana kadar çalışmaya devam eder (UseStaticFiles kalır); bu URL'lerde boy varyantı yoktur, olduğu gibi kullanılır.

## 2. İşleme (SkiaSharp 2.88.x — MIT lisanslı; ImageSharp ticari kullanımda lisans gerektirebildiği için seçilmedi)
- Linux'a yayın için `SkiaSharp.NativeAssets.Linux.NoDependencies` de referanslanır.
- Kabul: JPEG, PNG, WebP (imzaya göre — `SKCodec.Create` ile format tespiti; uzantıya güvenilmez). En fazla **10 MB**, en fazla 40 megapiksel (decompression bomb koruması: piksel çözülmeden önce `SKCodec.Info` ile boyut kontrolü).
- `SKCodec.EncodedOrigin`'e göre yön düzeltilir; pikseller yeniden kodlandığı için **tüm metadata (EXIF, GPS, XMP) atılır**.
- Boylar (uzun kenar, büyütme yok; avatar kare):

| kind | sm | md | lg |
|---|---|---|---|
| avatars | 128 | 320 | 640 |
| venues | 480 | 1200 | 1920 |
| messages | 480 | 1280 | 2048 |

- WebP kalite 82 (sm 75). Avatar kare değilse ortadan kare kırpılır (frontend zaten kırpıyor; sunucu da garanti eder).
- Hata: 400 `{ success:false, message:"Dosya geçerli bir resim değil." | "Fotoğraf en fazla 10 MB olabilir." | "Fotoğraf çok büyük (en fazla 40 MP)." }`.

## 3. Veri modeli

```
Media
  MediaId int PK
  Kind byte            1 Avatar, 2 VenuePhoto, 3 MessageImage
  KeyPrefix nvarchar(200)   ör. "avatars/2026/11/3f..9a"  (unique)
  OwnerPlayerId int?   yükleyen (FK Players, SetNull) — admin'in işletme adına yüklediğinde admin
  Width int, Height int     md varyantının boyutu
  Bytes int                 tüm varyantların toplamı
  Status byte          0 Pending (yüklendi, henüz bir kayda bağlanmadı), 1 Attached, 2 Deleted
  CreatedAt datetime2, AttachedAt datetime2?
```
Bağlantı kolonları (nullable FK yok — sadece int?, silme davranışı servis tarafından):
- `Players.ProfileMediaId int?` (ProfilePictureUrl kolonu kalır, md URL'sini tutar)
- `StadiumPhotos.MediaId int?` (Url kolonu kalır, md URL'sini tutar)
- `Messages.MediaId int?`, `Messages.ImageUrl nvarchar(300) null`
- `GroupMessages.MediaId int?`, `GroupMessages.ImageUrl nvarchar(300) null`

`IMediaService` (Api katmanında, IFileStorage + DbContext/DAL kullanır):
- `UploadAsync(IFormFile, MediaKind, int uploaderId)` → `MediaDto` (Status Pending)
- `Attach(mediaId, expectedKind, ownerId)` → Pending + aynı sahip + aynı tür değilse hata ("Fotoğraf bulunamadı.")
- `DeleteAsync(mediaId)` → depodan varyantları sil, Status=Deleted
- `ReplaceAvatar`, foto silme, hesap silme (Settings → hesap silme akışı) eski medyayı siler. Eski `/uploads` dosyası ise `ImageUploads.Delete` ile.

`MediaDto`: `{ mediaId, url (md), thumbUrl (sm), largeUrl (lg), width, height }`

### Temizlik (BackgroundService `MediaCleanupService`, saatte bir)
- 24 saatten eski **Pending** medya → depodan silinir, Status=Deleted.
- Status=Deleted ve 7 günden eski satırlar tablodan kaldırılabilir (opsiyonel).

## 4. Uç noktalar

### Yeni: `MediaController` (`[Authorize]`, rate limit politikası **"upload"**: kullanıcı başına dakikada 20)
| Yöntem | Yol | Gövde | Dönüş |
|---|---|---|---|
| POST | `/api/Media/Upload` | multipart: `file`, `kind` (`message` — şimdilik sadece mesaj resmi bu yoldan) | `{ success:true, mediaId, url, thumbUrl, largeUrl, width, height }` (Pending) |

### Değişen
- `POST /api/Players/SavePlayer` (kayıt) ve `POST /api/Players/UpdatePlayer`: `profilePicture` dosyası artık medya hattından geçer (kind avatars). Eskisi silinir. Dönüşte `profilePictureUrl` yeni md URL.
- `POST /api/Owner/Photos/Upload`, `POST /api/Admin/CreateOwner` (photos): kind venues. Dönüş şekli aynı (`url` = md URL). `Photos/Delete` medyayı da siler.
- `POST /api/Message/MessageSave`: gövdeye `mediaId?: int` eklenir. `matchMessage` boş olabilir **ancak** ikisinden biri dolu olmalı (400 "Mesaj boş olamaz."). mediaId: gönderene ait, Pending, kind=MessageImage olmalı → Attached; mesajın `ImageUrl`'i md URL olur.
- `POST /api/GroupsMessage/SaveGroupMessage`: aynı kural (`mediaId`).
- Mesaj okuma uçları (`GetMessageById`, `GetGroupMessageById`, `GetSendMessages`, `GetRecipientMessages`) her mesajda `imageUrl` (md URL veya null) döner.
- `GET /api/Social/MessageContacts` → `lastMessage.text` resimli ve metinsiz mesajda `"📷 Fotoğraf"`; ayrıca `lastMessage.hasImage: bool`.

### Geçiş (tek seferlik, admin)
- `POST /api/Admin/Media/MigrateLegacy` (Admin rolü) → `/uploads/...` ile başlayan tüm `Players.ProfilePictureUrl` ve `StadiumPhotos.Url` kayıtlarını wwwroot'tan okuyup hattan geçirir, yeni URL + MediaId yazar. Dosya bulunamazsa o kayıt atlanır ve raporda listelenir. Eski dosyalar **silinmez**. İdempotent.
  Dönüş: `{ success, migratedAvatars, migratedVenuePhotos, missing: [ "/uploads/..." ] }`

## 5. Frontend
- `antd-img-crop` (antd Upload ile entegre kırpma; arka planda react-easy-crop):
  - Profil fotoğrafı (kayıt + profil düzenleme): kare, yuvarlak önizleme (`cropShape="round"`), döndürme + yakınlaştırma.
  - Tesis fotoğrafı (işletme paneli): 16:10, döndürme + yakınlaştırma. Admin "işletme oluştur" çoklu yüklemede kırpma yok.
- `utils/image.js` → `prepareImage(file, { maxSide=2560, quality=0.9 })`: tarayıcıda küçültüp JPEG'e çevirir (mobil veride hızlı yükleme; HEIC dışındaki tüm resimler). Sunucu yine işler.
- `utils/format.js` → `mediaUrl(url, size = 'md')`: `/media/.../md.webp` ise `sm|md|lg` ile değiştirir; diğer URL'lerde olduğu gibi. `fileUrl` aynen kalır, `REACT_APP_MEDIA_BASE_URL` desteği eklenir.
  - `PlayerAvatar` → `sm` (büyük boyutlu avatarlarda ≥ 96 px `md`), tesis kartları → `md`, galeri/önizleme → `lg`. `<img loading="lazy" decoding="async">`.
- Mesaj paneli (`MessagesDrawer`): yazma alanında fotoğraf butonu + **panodan yapıştırma (Ctrl+V)** + sürükle-bırak; gönderim öncesi küçük önizleme ve kaldırma; yükleme sırasında ilerleme. Baloncukta resim (`sm`/`md`), tıklayınca antd `Image` önizleme (`lg`). Kişi listesinde son mesaj "📷 Fotoğraf". Grup sohbetleri de dahil.

## Uygulama notları (backend)

Uç nokta şekilleri sözleşmeyle aynıdır; aşağıdakiler sözleşmenin açık bıraktığı ayrıntılar / küçük eklemelerdir.

- **Paketler:** `SkiaSharp` 2.88.9, `SkiaSharp.NativeAssets.Linux.NoDependencies` 2.88.9, `AWSSDK.S3` 3.7.511.8
  (`RequestChecksumCalculation`/`ResponseChecksumValidation = WHEN_REQUIRED`, Put'ta `DisablePayloadSigning = true` — R2 akışlı SigV4'ü desteklemiyor).
- **Migration:** `MediaPipeline` (DataAccess). `Media` tablosu + `Players.ProfileMediaId`, `StadiumPhotos.MediaId`, `Messages.MediaId/ImageUrl`, `GroupMessages.MediaId/ImageUrl`.
- **SavePlayer / UpdatePlayer dönüşü:** `{ success, message, profilePictureUrl }` (önceden `{ success, message }`; alan eklendi).
- **Metinsiz resimli mesaj:** `matchMessage` DB'de `""` olarak saklanır (kolon NOT NULL kaldı); okuma uçlarında `matchMessage: ""`, `imageUrl: "/media/..."`.
  Okuma uçları `Message` varlığını döndüğü için `mediaId` alanı da gelir.
- **Hata kodları:** mediaId geçersiz / başkasının / zaten bağlı / tür yanlış → **400** "Fotoğraf bulunamadı.". Dosya yoksa 400 "Fotoğraf seçilmedi.".
  `Media/Upload` `kind` olarak `message` (veya `messages`) kabul eder; diğerleri 400.
- **İstek boyutu:** `Media/Upload` ve `Owner/Photos/Upload` 12 MB'a kadar istek kabul eder (10–12 MB arası dosya → 400 "Fotoğraf en fazla 10 MB olabilir.", 12 MB üstü → Kestrel **413**). `Admin/CreateOwner` 110 MB (10 × 10 MB).
- **Rate limit:** `"upload"` politikası (`RateLimiting:UploadPermitLimit`, varsayılan 20/dk, kullanıcı id'sine göre; token yoksa IP) `Media/Upload` ve `Owner/Photos/Upload`'a uygulanır.
  Bunun için `UseRateLimiter()` artık `UseAuthentication()`'dan sonra çalışıyor. 429 yanıtı artık gövde içerir: `{ success:false, message:"Çok fazla istek. Lütfen biraz sonra tekrar deneyin." }` ("auth" politikası için de).
- **İşletmenin eski profil fotoğrafı:** `CreateOwner` işletme hesabının `ProfilePictureUrl`'ini ilk tesis fotoğrafının md URL'si yapar (eski davranış), `ProfileMediaId` null kalır.
  Eski `/uploads` dosyası silinirken (avatar değişimi, foto silme, hesap silme) dosyayı başka bir `StadiumPhotos`/`Players` kaydı hâlâ kullanıyorsa dosya silinmez.
- **MigrateLegacy:** okunamayan **veya geçerli resim olmayan** dosyalar da `missing` listesine eklenir. İşletmenin profil fotoğrafı bir tesis fotoğrafıyla aynı dosyaysa ikisi ayrı ayrı geçirilir (avatar kare kırpılır). Tesis fotoğraflarının medya sahibi tesisin sahibidir.
  2026-11-06'daki gerçek çalıştırma: `migratedAvatars: 101`, `migratedVenuePhotos: 18`, `missing: []` (seed verisinde birçok oyuncu aynı dosyayı paylaşıyor); ikinci çalıştırma 0/0.
- **Temizlik:** `MediaCleanupService` açılışta ve sonra `Media:CleanupIntervalMinutes` (varsayılan 60) aralıkla çalışır; 24 saatten eski Pending → depodan silinir + Deleted.
  Opsiyonel madde de uygulandı: 7 günden eski Deleted satırlar, hiçbir bağlantı kolonunda kullanılmıyorsa tablodan silinir.
- **Yerel depo:** `src/Api/MatchBuddy.Api/App_Data/media` (`.gitignore`'a eklendi). İçerik kökü (content root) proje klasörü olmalı (VS / `dotnet run` varsayılanı).

## R2 kurulumu

Canlıda fotoğrafları Cloudflare R2'de tutmak için (kod değişikliği gerekmez, sadece ayar):

1. **R2'yi etkinleştir:** Cloudflare panelinde (dash.cloudflare.com) hesabını seç → sol menüden **R2 Object Storage** → ilk kullanımda **Purchase R2 / Enable R2** (ücretsiz katman için de ödeme yöntemi istenir). Sayfanın sağındaki **Account ID** değerini not al.
2. **Bucket oluştur:** R2 → **Create bucket** → ad: ör. `matchbuddy-media`, Location: *Automatic* (veya Avrupa için *EEUR* ipucu), Default storage class: *Standard* → **Create bucket**.
3. **Herkese açık erişim** (bucket → **Settings**):
   - Hızlı yol: **Public Development URL (r2.dev)** → **Allow Access** → onay kutusuna `allow` yaz. Verilen adres (ör. `https://pub-xxxxxxxx.r2.dev`) `PublicBaseUrl` olur. r2.dev hız sınırlıdır, sadece deneme için önerilir.
   - Canlı için: **Custom Domains** → **Connect Domain** → Cloudflare'de yönetilen bir alan adı gir (ör. `media.ornek.com`) → DNS kaydı otomatik eklenir, durum *Active* olunca `PublicBaseUrl = https://media.ornek.com`. (Önbellek kuralları için alan adının Cloudflare'de olması gerekir; dosyalar zaten `Cache-Control: public, max-age=31536000, immutable` ile yüklenir.)
4. **API token oluştur:** R2 ana sayfası → **Manage R2 API Tokens** (ya da *API → Manage API tokens*) → **Create API token** →
   Permissions: **Object Read & Write**, Specify bucket(s): **Apply to specific buckets only** → `matchbuddy-media`, TTL: isteğe göre → **Create API Token**.
   Çıkan **Access Key ID** ve **Secret Access Key** sadece bir kez gösterilir; kaydet. (S3 endpoint'i de burada görünür: `https://<ACCOUNT_ID>.r2.cloudflarestorage.com`.)
5. **Ayarları gir** (geliştirme makinesinde `src/Api/MatchBuddy.Api` klasöründe; değerleri kendi değerlerinle değiştir):

```powershell
cd src/Api/MatchBuddy.Api
dotnet user-secrets set "Storage:Provider" "S3"
dotnet user-secrets set "Storage:S3:ServiceUrl" "https://<ACCOUNT_ID>.r2.cloudflarestorage.com"
dotnet user-secrets set "Storage:S3:Bucket" "<BUCKET_ADI>"
dotnet user-secrets set "Storage:S3:AccessKey" "<ACCESS_KEY_ID>"
dotnet user-secrets set "Storage:S3:SecretKey" "<SECRET_ACCESS_KEY>"
dotnet user-secrets set "Storage:S3:PublicBaseUrl" "https://<PUBLIC_ALAN_ADI_YA_DA_PUB-xxx.r2.dev>"
```

   Sunucuda user-secrets yerine ortam değişkenleri: `Storage__Provider=S3`, `Storage__S3__ServiceUrl=...`, `Storage__S3__Bucket=...`, `Storage__S3__AccessKey=...`, `Storage__S3__SecretKey=...`, `Storage__S3__PublicBaseUrl=...`.
   (`Storage:S3:Region` varsayılan `auto`; R2 için değiştirme.)
6. API'yi yeniden başlat. Provider `S3` iken eksik ayar varsa API açılışta anlaşılır bir hatayla durur. `GET /media/{key}` istekleri `PublicBaseUrl/{key}`'e 302 ile yönlenir;
   frontend'de `REACT_APP_MEDIA_BASE_URL=<PublicBaseUrl>` tanımlanırsa yönlendirme hiç olmadan doğrudan R2'den yüklenir.
7. **Mevcut yerel dosyalar:** S3'e geçmeden önce yüklenmiş `/media/...` dosyaları `App_Data/media` altındadır; aynı klasör yapısıyla bucket'a kopyalanmalıdır (ör. `rclone copy src/Api/MatchBuddy.Api/App_Data/media r2:<BUCKET_ADI>` — rclone'da `provider = Cloudflare` ile yapılandırılmış bir remote).
