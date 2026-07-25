# Operasyon notları

## Secret’lar

| Anahtar | Açıklama | Development | Production |
|---------|----------|-------------|------------|
| `Jwt:SigningKey` | JWT imza (min 32 karakter) | `appsettings.Development.json` | Ortam değişkeni / User Secrets — `LOCAL_DEV_ONLY*` yasak |
| `Security:NationalIdHashPepper` | TCKN lookup hash pepper | Development JSON | Ortam değişkeni — `LOCAL_DEV_ONLY*` yasak |
| `Security:ProtectDataProtectionKeys` | DP anahtarlarını DPAPI ile şifrele | `false` | `true` (Windows) |
| `Security:ProtectKeysWithMachineKey` | DPAPI LocalMachine (IIS app pool) | — | `true` önerilir |
| `Seed:AdminPassword` | İlk admin şifresi (yalnızca admin yoksa) | `ChangeMe!123` | Zorunlu yapılandırma |
| `ConnectionStrings:DefaultConnection` | MSSQL | LocalDB | Sunucu connection string |

### User Secrets (geliştirici makinesi)

```bash
cd backend/src/PersonelYonetim.Api
dotnet user-secrets set "Jwt:SigningKey" "EN_AZ_32_KARAKTER_RASGELE_DEGER"
dotnet user-secrets set "Security:NationalIdHashPepper" "RASGELE_PEPPER"
dotnet user-secrets set "Seed:AdminPassword" "GucluSifre!123"
```

### Ortam değişkenleri (Plesk / IIS / Docker)

ASP.NET Core çift alt çizgi (`__`) ile hiyerarşi okur:

```text
Jwt__SigningKey=...
Security__NationalIdHashPepper=...
Security__ProtectDataProtectionKeys=true
Seed__AdminPassword=...
ConnectionStrings__DefaultConnection=...
ASPNETCORE_ENVIRONMENT=Production
```

## Oturum politikası

| Ayar | Varsayılan | Anlamı |
|------|------------|--------|
| `Jwt:AccessTokenMinutes` | 30 | Access JWT ömrü |
| `Jwt:RefreshTokenDays` | 1 | Sliding yenileme jetonu |
| `Jwt:RefreshTokenAbsoluteDays` | 7 | İlk girişten itibaren mutlak üst sınır |
| `Jwt:IdleTimeoutMinutes` | 30 | Hareketsizlik sonrası düşme (backend + SPA) |

Şifre sıfırlama veya kullanıcıyı pasifleştirme tüm refresh token’ları iptal eder.

## Migration

```bash
cd backend/src/PersonelYonetim.Api
dotnet ef database update --project ../PersonelYonetim.Infrastructure
```

Uygulama açılışında `DbSeeder` de `MigrateAsync` çalıştırır.

**Production:** örnek personel, demo TCKN ve örnek bildirim seed’i çalışmaz. Yalnızca yetki/rol/katalog/organizasyon + admin (yoksa).

## Yedekleme (otomatik)

Script: `scripts/Backup-PersonelYonetim.ps1`

```powershell
cd scripts
.\Backup-PersonelYonetim.ps1 `
  -SqlServer "SUNUCU" `
  -Database "PersonelYonetimDb" `
  -BackupRoot "D:\Backups\PersonelYonetim" `
  -ApiDataPath "C:\inetpub\PersonelYonetim\App_Data" `
  -RetentionDays 14
```

**Windows Görev Zamanlayıcı:** günlük 02:00’de bu script’i çalıştırın (en az Full Backup + `dp-keys` + `uploads`).

Manuel sqlcmd örneği:

```bash
sqlcmd -S "(localdb)\mssqllocaldb" -Q "BACKUP DATABASE [PersonelYonetimDb] TO DISK=N'C:\Backups\PersonelYonetimDb.bak' WITH INIT, COMPRESSION, CHECKSUM"
```

### Yedek geri yükleme (kısa test)

1. Uygulamayı durdurun.
2. `RESTORE DATABASE ... WITH REPLACE` (test sunucusunda denenecek).
3. `dp-keys` ve `uploads` klasörlerini yedekten geri kopyalayın.
4. Uygulamayı başlatıp giriş + bir TCKN görüntüleme + bir dosya açma ile doğrulayın.

**RPO hedefi (öneri):** günlük yedek → en fazla ~24 saat veri kaybı. Kritik dönemlerde günde 2+ yedek alın.

## Hassas erişim denetimi

`AuditLogs` aksiyonları (örnek):

- `ViewNationalId`, `ViewSpecialConditions`
- `ViewSpecialConditionDocument`, `ViewEmployeePhoto`
- `LoginFailed`, `LoginFailedUnknownUser`, `LoginLockout`, `LoginSuccess`

## Plesk / yayın

1. `ASPNETCORE_ENVIRONMENT=Production` ayarlayın.
2. Secret’ları panel ortam değişkenlerinden verin (repo’ya yazmayın).
3. CORS: `Cors:AllowedOrigins` içine canlı frontend URL ekleyin.
4. HTTPS zorunlu; HTTP→HTTPS yönlendirme açık kalsın.
5. `App_Data` yazılabilir olsun (uploads + dp-keys).
6. Windows’ta `ProtectDataProtectionKeys=true` bırakın.

## Şifre politikası

Kullanıcı oluşturma / şifre sıfırlama: en az 8 karakter; büyük harf, küçük harf, rakam ve özel karakter zorunlu.

## Lockout

5 başarısız giriş → 15 dakika kilit. Olaylar `AuditLogs` tablosuna yazılır (`LoginFailed`, `LoginLockout`, `LoginSuccess`, `LoginFailedUnknownUser`).

## CI

GitHub Actions: `.github/workflows/ci.yml` — `dotnet build` + `npm run build`.
