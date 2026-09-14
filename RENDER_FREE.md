# Render Free + Supabase Free kurulumu

Bu yapı tek bir Render Free Docker web servisiyle arayüzü ve API'yi aynı adresten sunar. PostgreSQL veritabanı, Data Protection anahtarları ve yüklenen dosyalar Supabase'de tutulur; Render'ın geçici diskine veri yazılmaz.

1. Supabase'de **Free** proje açın. **Storage** altında `personel-private` adlı **Private** bucket oluşturun. Public yapmayın. Project Settings / API Keys bölümünden `sb_secret_...` biçimindeki **secret key** değerini yalnızca Render ortam değişkeninde kullanın.
2. Supabase **Connect → Session pooler** bağlantısını açın. Host, kullanıcı ve portu buradan kopyalayın. **Database Settings → SSL** bölümünden kök sertifikayı indirin. Render için `ConnectionStrings__DefaultConnection` değerini Npgsql biçiminde girin: `Host=POOLER_HOST;Port=5432;Database=postgres;Username=postgres.PROJECT_REF;Password=DB_PASSWORD;SSL Mode=Require;Maximum Pool Size=10`. Uygulama, Npgsql'ın Supabase pooler sertifikasında verdiği yanlış eşleşme hatasını aşmak için TLS sertifika zincirini indirilen köke sabitleyerek ve sunucu adını ayrıca doğrulayarak `VerifyFull` ile aynı iki denetimi uygular. Direct/IPv6 veya Transaction pooler/6543 bağlantısını kullanmayın.
3. GitHub deposundaki `render.yaml` dosyasından yeni Blueprint kurun. `plan: free` ve Docker seçili olmalı. `sync: false` alanlarını Render arayüzünde doldurun:

   | Değişken | Değer |
   | --- | --- |
   | `ConnectionStrings__DefaultConnection` | Yukarıdaki Session pooler bağlantısı |
   | `Supabase__Url` | `https://PROJECT_REF.supabase.co` |
   | `Supabase__SecretKey` | `sb_secret_...` sunucu anahtarı |
   | `Supabase__Bucket` | `personel-private` |
   | `Supabase__DatabaseCaCertificateBase64` | Supabase veritabanı kök sertifikasının Base64 içeriği |
   | `Jwt__SigningKey` | Rastgele en az 32 karakter |
   | `Security__NationalIdHashPepper` | Ayrı bir rastgele değer |
   | `Seed__AdminPassword` | İlk `admin` hesabı için güçlü parola |
   | `Security__DataProtectionCertificateBase64` | PFX sertifikasının Base64 içeriği |
   | `Security__DataProtectionCertificatePassword` | PFX parolası |

4. PFX için yerel bilgisayarda bir sertifika oluşturup Base64'e dönüştürün. Sertifika, parolası, JWT anahtarı ve TCKN pepper'ı **kaybolmamalı veya değiştirilmemeli**; değişirse mevcut korunan TCKN alanları okunamaz. Bunları GitHub'a, sohbet mesajına veya istemci uygulamasına koymayın. Render ortam değişkenlerini kullanın. Sertifikanın özel anahtarını ayrı ve güvenli bir yerde yedekleyin.
   Supabase'den indirilen veritabanı kök sertifikası PEM dosyasını da `base64 < supabase-ca.pem | tr -d '\n'` ile Render değişkenine girin. Bu sertifika PFX'ten farklıdır.
5. Deploy bitince `https://SERVIS.onrender.com/api/health` 200 dönmeli. Aynı adreste giriş ekranı açılmalı. `admin` ile ilk girişten sonra parolayı değiştirin. Bir deneme dosyası yükleyip indirin; yeniden deploy ettikten sonra tekrar açıldığını doğrulayın. Gerçek personel verilerini ancak bu kontrol tamamlanınca yükleyin.

**Sınırlar:** Render Free 15 dakika hareketsizlikte uyur, uyandırma yaklaşık bir dakika sürebilir. Supabase Free 500 MB veritabanı, 1 GB dosya alanı sağlar ve düşük kullanımda duraklatılabilir. Supabase Free otomatik yedek sağlamaz; veritabanı ve dosyalar için düzenli dış yedek gerekir. Bu kurulum kalıcı depolama sağlar ancak kesintisiz hizmet veya yedek garantisi vermez. Hassas personel verileri için kurumun veri işleme ve yurt dışı aktarım kuralları ayrıca değerlendirilmelidir.
