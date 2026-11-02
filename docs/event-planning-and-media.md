# Etkinlik talepleri, afiş ve fotoğraflar

Etkinlik ekleme/düzenleme formunda ayrı `Talepler ve ihtiyaçlar` alanı (8.000 karakter) bulunur. Ses, ışık, ulaşım, ikram, talep eden birim ve teslim tarihleri serbest metin olarak kaydedilir. Eski istemciler bu alanı göndermediğinde mevcut talepler korunur; boş metin gönderilirse temizlenir.

Afiş tek görseldir; yenisi aynı afiş kaydını değiştirir. Fotoğraf galerisi etkinlik başına 20 dosya kabul eder. JPG, PNG ve WebP desteklenir; dosya başına 8 MB, tek yüklemede toplam 40 MB sınırı vardır. Uzantı/içerik imzası sunucuda kontrol edilir. Tüm toplu yükleme doğrulanmadan kayıt değiştirilmez. Görseller veritabanında saklanır; Render dosya sistemine bağlı değildir. Dosyalar normal API yetkilendirmesi ve etkinliğin birim/tesis kapsamı ile okunur. İçerik sunumunda önbellek kapalıdır. Silinen dosyanın içeriği temizlenir; denetim kaydında dosya byte'ları tutulmaz.

Formda seçilen dosyalar etkinlik kaydından sonra yüklenir. Yükleme başarısız olduğunda kaydedilen etkinliğin kimliği ve başarısız dosya seçimi korunur. Kaydet tekrar denenebilir; yeni etkinlik oluşturulmaz. Kaydedilmiş etkinlikte dosyalar detay ekranından da eklenebilir/değiştirilebilir/silinebilir. Serilerde dosyalar ilk etkinliğe eklenir; formda bu davranış açıklanır.

`20261030020000_AddEventMedia` SQL Server ve PostgreSQL için yalnızca yeni Requirements sütunu ile EventMedia tablosunu ekler. Mevcut kayıtlar değişmez. Memurun etkinlik erişimi açılmaz; okuma yetkisi olan kullanıcılar dosyaları görür, Events.Manage sahibi kullanıcılar kendi kapsamlarında yönetir.

Doğrulama: EventMediaTests dosya doğrulama, toplu yükleme atomikliği, afiş değiştirme/yeniden ekleme, galeri kotası, silinen etkinlik/dosya erişimi, yetki ve kapsam, taleplerin eski istemci güncellemelerinde korunması senaryolarını kapsar. Tarayıcıda 390, 820 ve 1440 pikselde oluşturma/düzenleme, önizleme, doğrulama, kimlik doğrulamalı dosya okuma ve başarısız yükleme sonrası tekrarsız kayıt denendi.

## Pratik etkinlik girişi

Form takviminde ilk ve son gün tıklanarak tarih aralığı seçilir. “3 günlük etkinlik” başlangıç dahil üç gün seçer; saatler ayrıca düzenlenir. Bitiş başlangıçtan önce olamaz. Kültür ve Kongre Merkezi, salonların bulunduğu gruptan “Merkezin tamamı” olarak da seçilebilir.

Mevcut bilgi notu yetkisi olan kullanıcılar formda Word (.docx) veya PDF (5 MB) ekleyebilir veya metin yazarak not oluşturabilir. Word alanları mevcut belge okuma servisiyle doldurulur; PDF ekinde özet isteğe bağlıdır. Etkinlik önce kaydedilir; dosya veya not işlemi başarısızsa aynı etkinlik kimliği korunur. Seride not ilk etkinliğe bağlanır.

Otomatik bildirim taraması 15 dakikada bir yayımlanmış/tamamlanmış etkinliklerde fotoğraf eksikliğini kontrol eder. Bitiş varsa bu zamanın geçmesi beklenir; bitişsiz tarih kayıtları İstanbul gün sonundan sonra değerlendirilir. İptal ve taslaklar dahil edilmez; afiş fotoğraf yerine sayılmaz. Etkinlik yönetme yetkisi olanlara genel yükleme hatırlatması gönderilir; bildirim etkinlik isimlerini açığa çıkarmaz ve kapsam denetimli listeye gider. Aynı hatırlatma yedi gün içinde tekrarlanmaz. Uygulama çalışırken tarama yapılır.

Doğrulama: 75 ön yüz testi ve EventPhotoReminderTests, EventMediaTests, InformationNotesTests kapsamında 14 arka uç testi başarılı. Arka uç testleri yerelde kurulu .NET 10 üzerinde DOTNET_ROLL_FORWARD=Major ile çalıştırıldı. Ön yüz üretim derlemesi başarılı. Canlı ortamda tarayıcı ve dağıtım doğrulaması yapılmadı.

## Müdür için görüntüleme

Müdür rolü etkinlik, proje, bilgi notu ve etkinlik görsellerini görüntüler; Events.Manage izni müdür rolünden kaldırılır. Eski JWT yazma iddiaları hem API yetkilendirmesinde hem CurrentUserService içinde reddedilir. Yeni oturum ve yenilemede de yazma izni üretilmez. İstemcide aynı kural merkezî hasPermission üzerinden uygulanır; oluşturma, düzenleme, silme, yükleme, rapor değiştirme ve durum değiştirme seçenekleri görünmez. Sistem yöneticisinin mevcut izinleri korunur.

Etkinlik dosyası görünümü tarih/yer/katılım kartları, bölüm bağlantıları, ekranda açılan bilgi notları, belge indirme ve büyütülebilir afiş/fotoğraflar içerir. Boş talepler ve boş katılımcı bölümleri görüntüleme ekranında yer kaplamaz; işlem geçmişi açılır bölümde sunulur.

Doğrulama: 78 ön yüz testi; 187 arka uç testi başarılı, ortam bağımlı 11 test atlandı. DirectorEventAccessTests eski oturum, sunucu yetkilendirmesi, erişim koruması ve tekrar çalıştırılabilir rol düzeltmesini kapsar.

## Tipografi ve etkinlik dosyası düzeni

Arayüz, Türkçe karakterleri destekleyen Inter 400–700 değişken fontunu yerel WOFF2 dosyalarından sunar; Latin ve Latin Extended alt kümeleri toplam yaklaşık 134 KB'dir. Lisans frontend/public/fonts/Inter-OFL.txt içindedir. Önceki harici font yükleme bağlantıları kaldırılmıştır.

Etkinlik dosyasında daha ölçülü başlık, sade tarih/tesis/katılım şeridi ve bölüm bağlantıları kullanılır. Masaüstünde bilgi notu ve afiş iki sütundadır; dar ekranlarda tek sütuna geçilir. Not açma/daraltma durumu, bölüm bağlantısının seçili görünümü ve afiş önizlemesi korunur. Müdürün görüntüleme sınırı değişmez.

Bilgi notları arşivi de aynı tipografi ve sade belge görünümünü kullanır. Sol belge listesi masaüstünde sabit konumda kalır; dar ekranda normal akışa geçer. Başlık, tarih/düzenleyen bilgileri ve metin ayrı bir okuma düzeninde sunulur; başlıkla aynı konu tekrar basılmaz, farklı konu ayrıca gösterilir. Hazırlayan bilgisi belgenin sonunda görünür. Bu sayfada aramayı tekrar eden üst arama alanı gösterilmez. Yetki kontrolleri ve indirme/kaydetme API'leri değişmez.
