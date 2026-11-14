# MatcBuddy — Backend

Halı saha maçı kurma, kadro tamamlama ve tesis yönetimi platformu MatcBuddy'nin API'si (ASP.NET Core, .NET 10, EF Core, SQL Server).

Mimari ve kurallar: [docs/architecture.md](docs/architecture.md) · API sözleşmeleri: [docs/](docs/)

## Geliştirme

Gereken: .NET 10 SDK (`global.json`), SQL Server LocalDB.

```powershell
# İlk kurulum: araçlar + veritabanı şeması
dotnet tool restore
.\scripts\run-api.ps1 -Migrate

# Sonraki çalıştırmalar
.\scripts\run-api.ps1
```

API: http://localhost:5033 · Swagger: http://localhost:5033/swagger

Gizli değerler (yalnızca yerelde, depoya girmez):

```powershell
cd src/MatcBuddy.Api
dotnet user-secrets set "Jwt:Key" "<en-az-32-karakter-rastgele>"
```

## Test

```powershell
dotnet test MatcBuddy.sln
```

## Canlıya yükleme

```powershell
powershell -ExecutionPolicy Bypass -File scripts\deploy-monsterasp.ps1 -SkipDatabase
```

Varsayılanlar: API `https://matcbuddy.runasp.net`, ön yüz `https://matcbuddy.com` (`-SiteUrl` / `-FrontendUrl` ile değiştirilebilir).
