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

**Production:** kurgusal personel, etkinlik, muhtar, okul, alan, demo TCKN, bildirim ve test kullanıcıları seed edilmez. Yalnızca şema, referans katalogları, yerleşim/ADNKS kaynakları, başlangıç organizasyonu ve ilk admin (yoksa) oluşturulur. Canlı yetki matrisi sonraki açılışlarda yeniden yazılmaz.

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

## Ücretsiz, tek adresli Azure deneme yayını

Sunucu veya kalıcı disk olmadan Render Free üzerinde **canlı personel verisi yayınlamayın**. Render Free yerel dosyaları yeniden başlatmada siler; yüklenen belgeler ve Data Protection anahtarları kaybolur. [Render'ın resmi sınırları](https://render.com/docs/free). Vercel Hobby de yalnızca kişisel/ticari olmayan kullanım içindir; belediye iş akışı için uygunluğunu ayrıca değerlendirin. [Vercel Hobby koşulları](https://vercel.com/docs/plans/hobby).

Ücretsiz deneme için API ve arayüz tek bir **Azure App Service Windows F1** uygulamasında, veriler ayrı bir **Azure SQL Database free offer** veritabanında çalışabilir. Azure hesabı ve aboneliği gerekir; aylık ücretsiz sınırlar aşılırsa ücret çıkmaması için SQL veritabanını **"Auto-pause the database until next month"** seçeneğiyle oluşturun. Azure SQL ücretsiz teklifinde ayda 100.000 vCore-saniye, 32 GB veri ve 32 GB yedek alanı bulunur. [Azure SQL ücretsiz teklif](https://learn.microsoft.com/en-us/azure/azure-sql/database/free-offer). F1 kaynak ve CPU kotaları nedeniyle bu kurulum yüksek kullanılabilirlik sunmaz; gerçek kurumsal kullanım için kapasite, yedekleme, veri işleme şartları ve kurum onayı ayrıca değerlendirilmelidir.

1. Azure'da SQL Database'i **Start free** ile oluşturun. Sunucu oturumunu ve veritabanı adını not edin. SQL ağ güvenlik duvarına yalnızca App Service'in gerekli çıkış IP'lerini ekleyin; bağlantı dizesinde `Encrypt=True;TrustServerCertificate=False` kullanın. Veritabanı için otomatik aylık durdurmayı seçin.
2. Windows **F1** App Service oluşturun. Ücretli plana yükselten `Always On`, özel alan adı veya başka bir eklentiyi etkinleştirmeyin. Varsayılan `azurewebsites.net` HTTPS adresini kullanın. App Service'in kalıcı `%HOME%` alanı dosyaları yeniden başlatmalarda korur; F1 depolama kotasını takip edin. [Azure App Service kalıcı alan](https://learn.microsoft.com/en-us/azure/app-service/operating-system-functionality).
3. App Service ortam değişkenlerini ayarlayın: `ASPNETCORE_ENVIRONMENT=Production`, `ConnectionStrings__DefaultConnection`, `Jwt__SigningKey` (en az 32 rastgele karakter), `Security__NationalIdHashPepper` (sabit rastgele değer), `Seed__AdminPassword` (güçlü ilk parola), `Security__DataProtectionKeysPath=D:\home\data\dp-keys`, `FileStorage__RootPath=D:\home\data\uploads`, `Security__DataProtectionCertificateBase64` ve `Security__DataProtectionCertificatePassword`. PFX sertifikasını özel anahtarıyla ve parolasıyla güvenli yedekleyin. Azure'da sertifika verilirse uygulama DPAPI yerine bunu kullanır; böylece uygulama farklı makineye taşınsa da mevcut kayıtlar okunabilir.
4. Mac/Linux'ta güncel .NET 9 SDK ile `scripts/package-azure-free.sh /mutlak/yol/yonetim-bilgi-azure.zip` komutu Vite arayüzünü ve Windows için kendi .NET çalışma zamanını içeren API'yi tek ZIP olarak hazırlar. Betik, .NET 9.0.20'den eski güvenlik yamalı çalışma zamanını reddeder; ayrı SDK kurduysanız `DOTNET_BIN=/yol/dotnet` verin. Azure CLI'da `az webapp deploy --resource-group <grup> --name <uygulama> --src-path <zip>` ile yayınlayın. ZIP'in kökünde `PersonelYonetim.Api.exe`, `web.config` ve `wwwroot/index.html` olmalıdır.
5. `https://<uygulama>.azurewebsites.net/api/health` SQL bağlantısını doğrulamalı. Ardından aynı adreste giriş, sayfa yenileme, oturum yenileme, doğrudan alt sayfa, dosya yükleme/indirme ve yeniden başlatmadan sonra veri/oturum korunmasını test edin. `/api/olmayan` gibi bilinmeyen API yolu `404` dönmelidir.

Yerel makinedeki mevcut SQL Server veritabanını otomatik olarak canlıya taşımayın. Test veritabanında göç ve veri doğrulaması tamamlanmadan gerçek personel kayıtlarını yüklemeyin.

.NET 9 desteği [10 Kasım 2026'da sona eriyor](https://dotnet.microsoft.com/en-us/platform/support/policy); bu tarihten önce .NET 10 LTS'ye geçiş ve yeniden test planlanmalıdır.

## Vercel arayüz + Render API yayını

Bu yol Windows/Plesk yayınından bağımsızdır. İlk canlı dağıtımdan önce **ayrı bir SQL Server test veritabanında** migration, ilk giriş, yenileme çerezi, yükleme/indirme ve yeniden başlatma testi yapın. Canlı veritabanında ilk dağıtımdan önce yedek alın.

### Render

1. Repo kökündeki `render.yaml` blueprint'ini kullanın. API `Dockerfile` ile derlenir; `/api/health` SQL Server bağlantısını da doğrular. Blueprint ücretli `starter` servisi ve `/var/data` kalıcı diski tanımlar. Disk çıkarılırsa yüklenen dosyalar ve TCKN şifreleme anahtarları yeniden dağıtımda kaybolur.
2. Blueprint'teki `sync: false` alanlarını Render panelinde doldurun: `ConnectionStrings__DefaultConnection` (harici SQL Server, TLS doğrulaması açık), `Jwt__SigningKey` (en az 32 rastgele karakter), `Security__NationalIdHashPepper` (rastgele, kalıcı), `Seed__AdminPassword` (güçlü ilk parola), `Security__DataProtectionCertificateBase64` (özel anahtarı içeren PFX'in base64 içeriği), `Security__DataProtectionCertificatePassword` ve `Cors__AllowedOrigins__0` (Vercel canlı origin'i). Secret'ları repo'ya yazmayın. PFX ve parolasını güvenli yedekte saklayın; aynı veritabanı ve `dp-keys` ile geri yükleme için aynı sertifika gerekir.
3. `Security__DataProtectionKeysPath=/var/data/dp-keys` ve `FileStorage__RootPath=/var/data/uploads` blueprint'te ayarlıdır. API bu yollar ve Linux'ta anahtar koruması olmadan Production'da başlamaz. SQL Server'ı Render API servisinin ağından erişilebilir kılın; veritabanı için ayrı yedekleme kurun.
4. Render servisinin HTTPS URL'sini ve `/api/health` yanıtını kaydedin. Sağlık kontrolü veritabanı erişimi yoksa `503` döner.

### Vercel

1. GitHub reposunu Vercel'e `frontend` kök diziniyle bağlayın. Node 22, `npm ci`, `npm run build`, çıktı `dist` kullanın.
2. `RENDER_API_ORIGIN` ortam değişkenine Render API'nin yalnızca HTTPS origin'ini girin (örnek: `https://ornek.onrender.com`; sonuna `/api` eklemeyin). `frontend/vercel.mjs`, `/api/*` isteklerini aynı tarayıcı origin'i üzerinden Render'a iletir ve SPA sayfalarını `index.html` ile açar. Değişken yoksa yapılandırma bilerek hata verir.
3. Canlı URL'yi Render'daki `Cors__AllowedOrigins__0` değerine yazın. Giriş, sayfa yenileme sonrası oturum yenileme, dosya yükleme/indirme ve doğrudan iç sayfa URL'si açma akışlarını canlı URL'den sınayın.

Canlıya geçmeden önce test veritabanındaki sonuçları doğrulayın. Production seed'i kurgusal kayıt yazmamalı; yalnızca `admin` hesabı oluşturulmalı. Girişten sonra ilk admin şifresini değiştirin.
