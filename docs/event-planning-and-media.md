# Etkinlik talepleri, afiş ve fotoğraflar

Etkinlik ekleme/düzenleme formunda ayrı `Talepler ve ihtiyaçlar` alanı (8.000 karakter) bulunur. Ses, ışık, ulaşım, ikram, talep eden birim ve teslim tarihleri serbest metin olarak kaydedilir. Eski istemciler bu alanı göndermediğinde mevcut talepler korunur; boş metin gönderilirse temizlenir.

Afiş tek görseldir; yenisi aynı afiş kaydını değiştirir. Fotoğraf galerisi etkinlik başına 20 dosya kabul eder. JPG, PNG ve WebP desteklenir; dosya başına 8 MB, tek yüklemede toplam 40 MB sınırı vardır. Uzantı/içerik imzası sunucuda kontrol edilir. Tüm toplu yükleme doğrulanmadan kayıt değiştirilmez. Görseller veritabanında saklanır; Render dosya sistemine bağlı değildir. Dosyalar normal API yetkilendirmesi ve etkinliğin birim/tesis kapsamı ile okunur. İçerik sunumunda önbellek kapalıdır. Silinen dosyanın içeriği temizlenir; denetim kaydında dosya byte'ları tutulmaz.

Formda seçilen dosyalar etkinlik kaydından sonra yüklenir. Yükleme başarısız olduğunda kaydedilen etkinliğin kimliği ve başarısız dosya seçimi korunur. Kaydet tekrar denenebilir; yeni etkinlik oluşturulmaz. Kaydedilmiş etkinlikte dosyalar detay ekranından da eklenebilir/değiştirilebilir/silinebilir. Serilerde dosyalar ilk etkinliğe eklenir; formda bu davranış açıklanır.

`20261030020000_AddEventMedia` SQL Server ve PostgreSQL için yalnızca yeni Requirements sütunu ile EventMedia tablosunu ekler. Mevcut kayıtlar değişmez. Memurun etkinlik erişimi açılmaz; okuma yetkisi olan kullanıcılar dosyaları görür, Events.Manage sahibi kullanıcılar kendi kapsamlarında yönetir.

Doğrulama: EventMediaTests dosya doğrulama, toplu yükleme atomikliği, afiş değiştirme/yeniden ekleme, galeri kotası, silinen etkinlik/dosya erişimi, yetki ve kapsam, taleplerin eski istemci güncellemelerinde korunması senaryolarını kapsar. Tarayıcıda 390, 820 ve 1440 pikselde oluşturma/düzenleme, önizleme, doğrulama, kimlik doğrulamalı dosya okuma ve başarısız yükleme sonrası tekrarsız kayıt denendi.
