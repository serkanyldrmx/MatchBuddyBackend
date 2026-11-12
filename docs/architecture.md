# MatcBuddy — Backend mimarisi

## Depolar

| Depo (yerel klasör) | İçerik |
| --- | --- |
| `MatcBuddy` | ASP.NET Core API (.NET 10), EF Core + SQL Server |
| `MatcBuddyReact` | Web istemcisi (React, CRA) — Vercel'de yayınlanır |
| `MatcBuddyMobile` | iOS kabuğu (Capacitor) — canlı web uygulamasını açar |

## Çözüm yapısı

```
MatcBuddy.sln
Directory.Build.props      ortak derleme ayarları (net10.0, nullable, implicit usings)
Directory.Packages.props   merkezi NuGet sürümleri — sürümler yalnızca burada değişir
src/
  MatcBuddy.Core           sonuç tipleri, hata kodları, yerelleştirme (resx), sistem parametreleri
  MatcBuddy.Entities       varlıklar ve DTO'lar
  MatcBuddy.DataAccess     DbContext (MatcBuddyContext), EF yapılandırmaları, migration'lar, DAL
  MatcBuddy.Business       iş kuralları (manager'lar), kimlik doğrulama
  MatcBuddy.Api            controller'lar, SignalR hub'ları, arka plan servisleri, dış entegrasyonlar
tests/
  MatcBuddy.Tests          mimari kurallar + yerelleştirme bütünlüğü
scripts/                   yerel çalıştırma, canlıya yükleme, demo verisi
docs/                      API sözleşmeleri (frontend ile ortak)
```

### Bağımlılık yönü

```
Core  ←  Entities  ←  DataAccess  ←  Business  ←  Api
```

Bağımlılıklar yalnızca sola doğru akar. `tests/MatcBuddy.Tests/ArchitectureTests.cs` bu kuralı her derlemede doğrular;
yanlış yönde bir proje referansı eklenirse CI kırılır.

## Kurallar

- **Paket sürümü** eklerken/yükseltirken `Directory.Packages.props` düzenlenir; csproj'a `Version` yazılmaz.
- **Kullanıcıya görünen her metin** `src/MatcBuddy.Core/Resources/Messages.resx` içinde, anahtarı `ErrorCodes` /
  `MessageKeys` sabitlerinde durur. `LocalizationTests` eksik anahtarı yakalar.
- **Migration** eklemek: `dotnet ef migrations add <Ad> --project src/MatcBuddy.DataAccess --startup-project src/MatcBuddy.Api`.
  Var olan migration kimlikleri (dosya adındaki zaman damgası + ad) veritabanındaki `__EFMigrationsHistory` tablosunda
  kayıtlıdır; **yeniden adlandırılmamalıdır**.
- **Gizli değerler** (JWT anahtarı, SMTP, Facebook secret, VAPID) depoya yazılmaz: geliştirmede `dotnet user-secrets`,
  canlıda `appsettings.Production.json` (deploy betiği üretir) ya da ortam değişkenleri.

## Büyüme yol haritası (önerilen sıra)

Proje büyüdükçe aşağıdaki ayrımlar, canlıyı durdurmadan tek tek yapılabilir:

1. **`MatcBuddy.Infrastructure`**: `Api/Services` altındaki dış entegrasyonları (e-posta/SMS, Web Push, medya/S3,
   hava durumu, OSM) ayrı projeye taşımak. Api yalnızca HTTP katmanı olarak kalır.
2. **Modüller**: Alanlar (Maç, Tesis/İşletme, Sosyal, Turnuva, Mesajlaşma) zaten `ErrorCodes.*` / `MessageKeys.*`
   ile ayrılmış durumda; her alan kendi Business + DataAccess klasörüne (ileride projesine) taşınabilir.
3. **Arka plan işleri**: `MatchReminderService`, `PushDispatchService`, `MediaCleanupService` yük arttığında ayrı bir
   Worker servisine (`MatcBuddy.Worker`) alınabilir; böylece API ölçeklenirken işler tek kopyada çalışır.
4. **Entegrasyon testleri**: `WebApplicationFactory` + test veritabanı (Testcontainers / SQL Server) ile uç nokta testleri.
