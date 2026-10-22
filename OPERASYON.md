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

Render Free'nin yerel diski kalıcı değildir; dosyaları veya Data Protection anahtarlarını burada tutmayın. Güncel ücretsiz kurulum, Supabase PostgreSQL ve özel Storage bucket kullanır; [RENDER_FREE.md](RENDER_FREE.md) dosyasına bakın. [Render'ın resmi sınırları](https://render.com/docs/free).

Ücretsiz deneme için API ve arayüz tek bir **Azure App Service Windows F1** uygulamasında, veriler ayrı bir **Azure SQL Database free offer** veritabanında çalışabilir. Azure hesabı ve aboneliği gerekir; aylık ücretsiz sınırlar aşılırsa ücret çıkmaması için SQL veritabanını **"Auto-pause the database until next month"** seçeneğiyle oluşturun. Azure SQL ücretsiz teklifinde ayda 100.000 vCore-saniye, 32 GB veri ve 32 GB yedek alanı bulunur. [Azure SQL ücretsiz teklif](https://learn.microsoft.com/en-us/azure/azure-sql/database/free-offer). F1 kaynak ve CPU kotaları nedeniyle bu kurulum yüksek kullanılabilirlik sunmaz; gerçek kurumsal kullanım için kapasite, yedekleme, veri işleme şartları ve kurum onayı ayrıca değerlendirilmelidir.

1. Azure'da SQL Database'i **Start free** ile oluşturun. Sunucu oturumunu ve veritabanı adını not edin. SQL ağ güvenlik duvarına yalnızca App Service'in gerekli çıkış IP'lerini ekleyin; bağlantı dizesinde `Encrypt=True;TrustServerCertificate=False` kullanın. Veritabanı için otomatik aylık durdurmayı seçin.
2. Windows **F1** App Service oluşturun. Ücretli plana yükselten `Always On`, özel alan adı veya başka bir eklentiyi etkinleştirmeyin. Varsayılan `azurewebsites.net` HTTPS adresini kullanın. App Service'in kalıcı `%HOME%` alanı dosyaları yeniden başlatmalarda korur; F1 depolama kotasını takip edin. [Azure App Service kalıcı alan](https://learn.microsoft.com/en-us/azure/app-service/operating-system-functionality).
3. App Service ortam değişkenlerini ayarlayın: `ASPNETCORE_ENVIRONMENT=Production`, `ConnectionStrings__DefaultConnection`, `Jwt__SigningKey` (en az 32 rastgele karakter), `Security__NationalIdHashPepper` (sabit rastgele değer), `Seed__AdminPassword` (güçlü ilk parola), `Security__DataProtectionKeysPath=D:\home\data\dp-keys`, `FileStorage__RootPath=D:\home\data\uploads`, `Security__DataProtectionCertificateBase64` ve `Security__DataProtectionCertificatePassword`. PFX sertifikasını özel anahtarıyla ve parolasıyla güvenli yedekleyin. Azure'da sertifika verilirse uygulama DPAPI yerine bunu kullanır; böylece uygulama farklı makineye taşınsa da mevcut kayıtlar okunabilir.
4. Mac/Linux'ta güncel .NET 9 SDK ile `scripts/package-azure-free.sh /mutlak/yol/yonetim-bilgi-azure.zip` komutu Vite arayüzünü ve Windows için kendi .NET çalışma zamanını içeren API'yi tek ZIP olarak hazırlar. Betik, .NET 9.0.20'den eski güvenlik yamalı çalışma zamanını reddeder; ayrı SDK kurduysanız `DOTNET_BIN=/yol/dotnet` verin. Azure CLI'da `az webapp deploy --resource-group <grup> --name <uygulama> --src-path <zip>` ile yayınlayın. ZIP'in kökünde `PersonelYonetim.Api.exe`, `web.config` ve `wwwroot/index.html` olmalıdır.
5. `https://<uygulama>.azurewebsites.net/api/health` SQL bağlantısını doğrulamalı. Ardından aynı adreste giriş, sayfa yenileme, oturum yenileme, doğrudan alt sayfa, dosya yükleme/indirme ve yeniden başlatmadan sonra veri/oturum korunmasını test edin. `/api/olmayan` gibi bilinmeyen API yolu `404` dönmelidir.

Yerel makinedeki mevcut SQL Server veritabanını otomatik olarak canlıya taşımayın. Test veritabanında göç ve veri doğrulaması tamamlanmadan gerçek personel kayıtlarını yüklemeyin.

.NET 9 desteği [10 Kasım 2026'da sona eriyor](https://dotnet.microsoft.com/en-us/platform/support/policy); bu tarihten önce .NET 10 LTS'ye geçiş ve yeniden test planlanmalıdır.

## Render Free + Supabase yayını

Güncel adımlar ve sınırlar için [RENDER_FREE.md](RENDER_FREE.md) dosyasına bakın. Arayüz ile API aynı Render servisinde çalışır; veritabanı ve özel dosyalar Supabase'de tutulur. İlk canlı veri aktarımından önce migration, giriş, dosya yükleme/indirme ve yeniden dağıtım sonrasında erişim testlerini tamamlayın.

## Personel zimmetleri

`/custodies` ekranı ve personel detayındaki **Zimmetli malzemeler** sekmesi, `Stock.View`, `Stock.CustodyManage` ve mevcut `Stock.Manage` izinlerini ve birim kapsamını kullanır. Görüntüleme `Stock.View`, atama/devir/iade `Stock.CustodyManage` veya mevcut `Stock.Manage` izni gerektirir. Müdür rolüne yalnızca zimmet yönetimi eklenir; genel stok yazma yetkisi açılmaz. Yeni yetki, oturum sayfa yenilemesiyle tazelendiğinde arayüze yansır.

- Malzeme kartında `Kind`: `0 = Belirtilmedi`, `1 = Sarf`, `2 = Demirbaş`. Kategori alanı bundan bağımsızdır. Yeni migration mevcut malzemeleri `Belirtilmedi` bırakır; isim veya kategoriye bakarak otomatik sınıflandırma yapılmaz. Zimmet açmadan önce ilgili malzemeyi **Demirbaş** olarak işaretleyin. Eski istemciler tür alanı göndermeden malzeme düzenlerse mevcut tür korunur.
- Zimmet yalnızca aktif personele ve aktif demirbaş malzemeye açılır. Kaynak stok yeri ve personel, işlemi yapan kullanıcının kapsamı içinde olmalıdır. Seri/demirbaş numarası isteğe bağlıdır; girilirse miktar 1 olmalıdır. Aynı ürün ve seri numarası aynı anda iki aktif zimmette kullanılamaz.
- `StockBalance.Quantity` fiziksel toplamdır. **Kullanılabilir = toplam − aktif zimmetler**. Zimmet, fiziksel miktarı azaltmadan kullanılabilir stoğu ayırır. İade aynı kaynak yerin kullanılabilir miktarını serbest bırakır; stok miktarına tekrar ekleme yapmaz. İade malzemesini farklı yere taşımak için sonrasında stok transferi kullanın.
- Değiştir/devret, eski kaydı iade ile kapatıp yeni kayıt açar. Önceki personel, malzeme, miktar, seri ve açıklama korunur; iki kayıt `ReplacesId` ile bağlanır. Değişim başarısız olursa eski zimmet de açık kalır. Miktar azaltma, kalan miktarla yeni zimmet açarak yapılabilir; önceki miktarın tamamı geçmişte görünür.
- Zimmetli miktar stok çıkışında veya transferinde kullanılamaz. Sayım, aktif zimmet miktarının altına indirilemez. Aktif zimmetli ürün arşivlenemez, pasifleştirilemez; türü veya ölçü birimi değiştirilemez. Personelin ayrılması/arşivlenmesi zimmetini otomatik kapatmaz; geçmişteki kişi bilgileri ve iade olanağı korunur.
- Stok yazma işlemleri ve zimmetler aynı `Serializable` işlem izolasyonunu kullanır. Atama isteğindeki istemci tarafından üretilen `Id`, cevabı kaybolan aynı işlemin tekrarında ikinci zimmet oluşturulmasını önler. İade aynı tarih/açıklamayla tekrarlandığında ek stok oluşturmaz. Zimmet değişiklikleri mevcut audit interceptor üzerinden kaydedilir.

**Yayın:** SQL Server ve PostgreSQL için ayrı `AddStockCustody` migration dosyaları vardır. Değişiklik mevcut stok satırlarını silmez veya miktarlarını dönüştürmez; `Kind` alanını ve `StockCustodies` tablosunu ekler. Mevcut açılış davranışı nedeniyle migration uygulama başlarken çalışır. Yayın öncesi yedek ve test ortamı doğrulaması yapın. Geri dönüşte uygulama kodunun önceki sürümüne dönmek yeni zimmet verilerini veritabanında korur; `Down` migration zimmet tablosunu kaldırdığı için gerçek zimmetler oluştuktan sonra veri kaybı olmadan uygulanamaz.

**Test:** Normal `dotnet test backend/PersonelYonetim.sln` yerel SQLite üzerinde iş kuralları, yetki, değişim rollback ve tekrar istek senaryolarını da çalıştırır. Gerçek PostgreSQL entegrasyon testleri yerelde varsayılan olarak atlanır; GitHub Actions backend işi geçici PostgreSQL servisini başlatarak bu testleri de çalıştırır. Bunları çalıştırmak için yalnızca geçici bir yerel PostgreSQL sunucusuna işaret eden `YBS_TEST_POSTGRES_CONNECTION` ayarlayın (`Host=127.0.0.1;Port=<test-port>;Database=postgres;Username=<test-user>;Password=<test-password>`). Test kullanıcısı test veritabanları oluşturup kaldırabilmelidir. Testler benzersiz `ybs_custody_test_*` veritabanları oluşturur ve kaldırır; uzak sunucu kabul edilmez. Bu testler mevcut migration'dan yükseltmeyi, aynı son ürüne eşzamanlı atamaları, zimmet/çıkış yarışını, yinelenen istekleri ve seri numarasıyla değişim rollback'ini doğrular.

### 22.10.2026 tarihinde istenen stok ve personel atamaları

Örnek ürünler mevcut aktif kadroya ve bu personellerin kayıtlı tesislerine bağlanır; yeni A/B personel veya deneme deposu oluşturulmaz. Varsayılan hedefler kadroda mevcutsa dizüstü bilgisayar için Ahmet Oral, telsiz için Abdullah Orhan, kulaklık için Ali Döner'dir. Bir kişi aktif ve geçerli tesisli değilse yalnızca mevcut aktif, geçerli tesisli kadro içinden uygun başka kayıt seçilir; isim veya kişi uydurulmaz. Hiç uygun personel yoksa atama ertelenir.

İlk sürümün `[ÖRNEK]` A/B kayıtlarından yalnızca henüz kullanıcı tarafından devredilmemiş/iade alınmamış ilk atamalar taşınır. Eski zimmet kapanır ve yeni personele bağlı kayıt açılır. Stok, örnek depodan gerçek tesise transfer edilir; toplam miktar artırılmaz. Ürün adları sadeleştirilir; örnek stokların kimliği `ORNEK-ZIM-*` kodlarıyla korunur. Manuel iadeler, sonraki devirler ve kullanıcıya ait başka stok kodları korunur. Aktif zimmeti veya kullanıcı hesabı kalmayan A/B personeller listeden kaldırılır; geçmiş ve audit kayıtları silinmez. Boş kalan deneme tesisi/birimi de listeden kaldırılır.

`Stock.RealPersonnelCustodies20261022` düzeltmenin bir defa çalıştığını tutar. `Stock.CustodyManagementPermission20261022`, müdür ve sistem yöneticisine yeni zimmet yönetim yetkisinin bir defa tanımlandığını tutar; sonradan yetki matrisinden yapılan değişiklikler yeniden açılışta geri alınmaz. İlk sürümün `Stock.RequestedCustodySamples20261022` tamamlanma kaydı geçmiş uyumluluğu için korunur; ilk A/B oluşturucu artık uygulama açılışında çağrılmaz.

Aktif zimmette **Personeli değiştir**, malzeme/seri/miktar/kaynak yeri koruyarak yeni personel ve devir tarihi seçtirir. **Malzemeyi değiştir** ayrı bir işlemdir. Devir, mevcut atomik iade + yeni zimmet akışını kullanır; eski personel kaydı geçmişte korunur. Örnek eklemesinin tamamlanma kaydını kaldırmak, sonraki açılışta tekrar deneme yapılmasına yol açacağı için normal temizlik yöntemi değildir.
