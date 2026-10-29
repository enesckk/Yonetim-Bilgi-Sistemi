# BilimFest Gaziantep 2026 projesi ve bilgi notu

Kaynak: kullanıcının 30 Ekim 2026 tarihinde son gönderdiği Word belgesi. SHA-256: `5219b7780734fda9194f80f3a74f502b527c1676a0636a3bb70b35c8458e7285`.

- Proje: TÜBİTAK 4007 – BilimFest Gaziantep 2026.
- Tarih: 15–17 Ekim 2026; saat verilmediği için yalnızca tarih.
- Durum: tamamlandı; kategori: proje.
- Tesis: mevcut KKM kodlu Şehitkamil Kültür ve Kongre Merkezi.
- Gerçekleşen katılım: belgede bildirilen 57.678 öğrenci. 87 atölye bilgisi açıklamada korunur; 87 ayrı proje veya her gün için yinelenmiş katılım oluşturulmaz.
- Mahalle dağılımı ve isim listesi verilmediği için bunlar üretilmez.
- Bilgi notu: tarih, düzenleyen kurum, konu, hazırlayan, beş açıklama paragrafı ve özgün Word dosyası korunur. Word içeriği kaynak özetiyle doğrulanır.

Yeni `InformationNotes` tablosunda bilgi notu metni ve isteğe bağlı belge özel olarak saklanır. SQL Server ve PostgreSQL için eklemeli migration ve model snapshot'ları bulunur. İşlem 1 proje + 1 bilgi notunu tek transaction içinde ekler; kayıt sonrası kaynak/metin/dosya/katılım doğrulamasından sonra makbuz yazılır. Yeniden başlatmada çoğalmaz veya kullanıcı düzenlemelerini ezmez. Çelişen mevcut proje veya eksik tesis halinde işlem geri alınır.

Bilgi notları müdürlük kapsamındaki `Events.View` kullanıcılarına açıktır; ekleme/düzenleme/silme için ayrıca `Events.Manage` gerekir. Kapsam dışı kullanıcılar liste, tam metin ve dosyalara erişemez. Dosyalar public statik dizine konmaz; dosya verileri işlem günlüklerinde maskelenir. Belge sınırı 5 MB, uzantı/içerik denetimi Word ve PDF içindir. Not silme soft delete uygular. Word yüklemede alanlar kaydetmeden önce okunup düzenlenebilir. Özgün belge ve bilgi notu alanlarını içeren Excel indirilebilir.

Etkinlik/proje ekranı tamamlanan kayıtlarla açılır; yaklaşanlar üstte gösterilir. Etkinlik/gezi ve proje sekmeleri ayrı, tarih/tür filtreleri açılır bölümdedir. Satırdan detay veya bilgi notu açılır; düzenleme ve silme detay ekranında korunur. Bilgi notları müdürlük menüsünden ve proje detayından açılabilir.

Kontroller: özgün belge ve tüm paragrafların korunması; KKM bağlantısı, tarih aralığı ve 57.678 katılım; idempotence ve rollback; not ekleme/düzenleme/soft delete; dosya yetkileri ve içerik sınırı; Excel alanlarının tam metin eşleşmesi; iki veritabanının migration/model eşleşmesi. Tarayıcıda 1440/390/820 piksel, tamamlanan varsayılanı, yaklaşan kartlar, proje ayrımı, Word otomatik alan okuma, yüklenen belgenin SHA-256 özeti, proje bağlantısı ve indirmeler kontrol edilir.
