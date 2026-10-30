# Etkinlik faaliyet raporları

Etkinlik modülündeki `/events/reports` menüsü müdürlük kapsamındaki Events.View kullanıcılarına açılır; oluşturma ve düzenleme ayrıca Events.Manage ister. Memur veya birim kapsamıyla sınırlı hesapların API, rapor dosyaları ve görsellere erişimi yoktur. Müdürlük/personel raporları değişmez.

## Kaynak ve doğruluk

Yıl veya özel tarih aralığı seçilir. Varsayılan ölçüt etkinlik başlangıç tarihi, isteğe bağlı ölçüt sisteme kayıt tarihidir. Tarih sınırları Türkiye saatiyle başlangıç dahil, bitiş gününün ertesi gece yarısı hariç hesaplanır. Tüm türler, etkinlik/gezi veya yalnız projeler seçilebilir. Silinmiş kayıtlar alınmaz. Taslak, planlanan, yapılan, iptal kayıtları gerçek durum etiketleriyle korunur; yapılmış kabul edilmez.

Toplam katılım öncelikle ActualAttendees, yoksa kayda bağlı mahallelerin girilmiş katılımlarından alınır. ExpectedAttendees ve katılımcı isim listesi sayısı gerçekleşmiş katılım olarak kullanılmaz. Tekil faydalanıcı sayısı gibi sunulmaz. Katılım yoksa sıfır kişi iddiası yerine bilgi girilmediği gösterilir. Çift saymayı engellemek için etkinlikte ve mahallede aynı katılım varsa yalnız etkinlik değeri alınır.

Açıklamalar ve bağlı bilgi notları eksiksiz kopyalanır. Aynı bilgi notu zaten açıklama olarak mevcutsa tekrar eklenmez. Uzun metinler devam sayfalarına bölünür; metin kesilmez. Excel hücre sınırını aşan açıklamalar devam satırlarına alınır, katılım yalnız ilk satıra yazılır. Aylık grafik seçilen tarih ölçütünü kullanır. Kaynak kayıt ve katılım toplamları düzenleyiciden değiştirilemez.

## Görseller ve düzenleme

Etkinlik galerisinde afiş veya fotoğraf üzerinde **Faaliyet raporunda kullan** kutusu bulunur. İşaretleme doğrudan kaydedilir; başarısız istek arayüzde geri alınır. Yeni/değiştirilen görsel varsayılan olarak işaretli değildir. Yalnız işaretli görseller yeni rapora alınır; 2025 örneklerindeki görseller 2026 verisi gibi eklenmez. Seçilmiş görsel yoksa rapor bunu bildirir, fotoğraf uydurmaz.

Rapor oluşturulurken metinlerin ve görsellerin bağımsız kopyası alınır. Etkinliğin sonradan değişmesi, görselin değiştirilmesi/silinmesi eski raporu bozmaz. Düzenleyici başlık, açıklama, tarih/yer metni, kurum/müdürlük, renk, sayfa sırası, sayfa ekleme/kaldırma, görsel ekleme/değiştirme/kaldırma, arşivden seçme ve tek/çift görsel veya metin düzenini destekler. Kaynak etkinlik değiştirilmez. Her sayfa en fazla 4 görsel kullanır; görsel başına 8 MB, rapor görsellerinde toplam 100 MB sınırı vardır.

Kaydet revision denetimiyle eşzamanlı düzenlemelerde üzerine yazmayı engeller. Hatalı kaydetme arayüzdeki düzenlemeyi silmez. Yenileme ve bağlantıyla ayrılma öncesi kaydedilmemiş değişiklik uyarısı bulunur. PDF/Excel indirirken bekleyen düzenleme varsa önce kaydedilir. Excel, düzenlenmiş anlatım sayfalarından bağımsız olarak raporun değişmez kaynak listesini dışa aktarır.

**Güncel verilerle yeni kopya** kaynakları yeniden okur ve yeni rapor oluşturur; önceki rapor korunur. Sunum görünümü tam ekran, önceki/sonraki sayfa, ok tuşları ve Escape ile kullanılır. PDF A4, tekrarlanan üst/alt bilgi ve otomatik sayfa numarasıyla sunucuda üretilir; uzun metinler taşmak yerine ek PDF sayfalarına akar. Gönderilecek özgün kurumsal şablon daha sonra uyarlanabilir; mevcut kurumsal görünüm referans ekranların metin/görsel düzenini temel alır.

## 2026 başlangıç raporu ve dağıtım

`ActivityReport2026Seed` sabit `c6e39703-ae9c-44f8-950b-202600000001` kimliğiyle 2026 başlangıç raporunu bir kez üretir. Canlı veritabanındaki o yılın tüm etkinlik/projelerini kullanır. Var olan rapora veya kullanıcı düzenlemesine tekrar yazılmaz. Daha sonra girilen kayıtlar yeni kopyayla alınır. Başlangıç üretimi diğer etkinlik/veri içe aktarımlarından sonra çalışır. PostgreSQL/SQL Server kilidi iki örneğin aynı raporu oluşturmasını engeller.

SQL Server ve PostgreSQL için `20261030030000_AddActivityReports` yalnız ActivityReports, ActivityReportAssets ve EventMedia.IncludeInReport alanını ekler. Mevcut kayıtları değiştirmez. Binary dosyalar ve tam JSON belge audit kopyasına alınmaz; metadata ve revision hareketi izlenir.

## Doğrulama

ActivityReportTests tarih sınırları, gerçekleşen/beklenen ayrımı, açıklama ve bilgi notlarının eksiksizliği, seçili fotoğrafların kopyalanması, kaynak silinmesine dayanıklılık, sayfa düzenlemelerinin saklanması, stale revision/başka rapor görseli reddi, erişim kısıtları, kayıt tarihine göre seçim, 2026 raporunun tekrar çalışmada korunması ve gerçek PDF/Excel üretimini kapsar. Gerçek içe aktarma kaynaklarıyla 34 çocuk tiyatrosu + 1 BilimFest = 35 kayıt ve 65.678 toplam katılım doğrulanmıştır; canlıda varsa diğer 2026 kayıtları da alınır. İki sağlayıcının migration/model tutarlılığı mevcut testle kontrol edilir. 390/820/1440 piksel tarayıcı denemelerinde oluşturma, düzenleme, sıralama/silme, görsel değiştirme/işaretleme, hatalı kaydetmede metnin korunması, sunum, PDF/Excel indirme, kaynak/önceki kopyanın korunması ve salt okuma rolü doğrulanır. PostgreSQL entegrasyon sunucusu olmadığı için mevcut 11 canlı PostgreSQL testi atlanır; bu sınırlama test raporunda gösterilir.
