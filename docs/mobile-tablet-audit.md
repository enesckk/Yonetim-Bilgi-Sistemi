# Telefon ve tablet görünüm incelemesi

Tarih: 28 Ekim 2026

## Kapsam ve yöntem

Chromium tabanlı tarayıcıda dokunmatik ekran benzetimiyle, hem dikey hem yatay yerleşimler kontrol edildi. Fiziksel iPhone/iPad/Android cihazlarında veya Safari motorunda test yapılmadı. Boyutlar CSS pikselidir.

| Görünüm | Boyut |
|---|---|
| Küçük telefon | 320 × 568 |
| Telefon | 360 × 800 |
| Telefon | 390 × 844 |
| Büyük telefon | 430 × 932 |
| Tablet, dikey | 768 × 1024 |
| Tablet, dikey | 820 × 1180 |
| Telefon, yatay | 844 × 390 |
| Büyük telefon, yatay | 932 × 430 |
| Tablet, yatay | 1024 × 768 |
| Büyük tablet, yatay | 1180 × 820 |

İncelenen ekranlar:

- Müdürlük özeti, personel listesi ve personel detayı.
- Personel ekleme/düzenleme: kimlik, iletişim, kurumsal bilgiler, tarihler, unvan/görev seçimi ve serbest yazı.
- Stok, stok yeri detayı, malzeme ekleme penceresi.
- Zimmet listesi ve zimmet ekleme penceresi.
- Tesis kartları, tesis listesi ve tesis detay çekmecesi.
- Organizasyon şeması ve dokunarak kayıt seçimi.
- Bildirimler, mesaj listesi, uzun metin/bağlantı içeren konuşma.
- Hesap bilgileri penceresi.
- Müdür/admin görünümlerinde etkinlik takvimi, etkinlik listesi, harita, etkinlik tesisleri ve etkinlik formu.

Memur ekranlarında mevcut verilere yalnızca okuma istekleri yapıldı. Ekleme/düzenleme ve müdür/admin ekranlarında örnek veriler kullanıldı; canlı personel, stok, zimmet veya mesaj kayıtları değiştirilmedi. Sistem yönetimi ekranlarının tamamı bu tarayıcı turuna dahil değildir.

## Düzeltilen sorunlar

| Bulgu | Düzeltme |
|---|---|
| Mobil menü açıkken arka sayfa kayabiliyordu. | Menü açıkken arka sayfanın kaydırması kilitleniyor, kapanınca önceki durum geri yükleniyor. |
| Menüde görünür bir kapatma düğmesi yoktu ve Escape çalışmıyordu. | Dokunulabilir kapatma düğmesi ve Escape desteği eklendi. |
| Menü kapalıyken bağlantıları klavye odağına girebiliyordu. | Kapalı mobil menü `inert` ve erişilebilirlik durumuyla devre dışı bırakıldı. |
| Açık menüde klavye odağı arka sayfaya kaçabiliyordu. | Tab/Shift+Tab menü içinde dolaşıyor; kapatıldığında odak açma düğmesine dönüyor. |
| Tablet yataya çevrildiğinde mobil menünün durumu açık kalabiliyordu. | Menü masaüstü yerleşimine geçildiğinde kapatılıyor ve kaydırma serbest bırakılıyor. |
| Birçok giriş alanı 12–14 px yazı kullanıyordu. | Telefon/tablet ve dokunmatik yerleşimlerde girişler 16 px yapıldı; odak sırasında mobil tarayıcının yakınlaştırma ihtimali azaltıldı. |
| Personel satır işlemleri, bazı kart başlıkları ve zimmet işlemleri küçük dokunma alanlarına sahipti. | Normal işlem düğmeleri ve girişler en az 44 px yüksekliğe getirildi. Yakınlaştırılabilir organizasyon şemasındaki düğümler bu fiziksel boyut ölçümünden ayrı değerlendirildi. |
| Hesap gibi sayfa dışına açılan pencereler genel mobil kuralları almıyordu. | Portal ile açılan hesap, onay, bildirim ve satır işlem pencereleri de boyut kurallarına dahil edildi. |
| Yatay kaydırılabilen stok/zimmet/tesis listelerinde devam eden sütunlar kolay fark edilmiyordu. | İlgili telefon/tablet yerleşimlerine kaydırma açıklaması ve odaklanabilir tablo bölgeleri eklendi. Mobilde kartlara dönüşen zimmet listesinde gereksiz açıklama gösterilmiyor. |
| Telefon yatayken mesaj listesi üstte kalıp konuşma alanını daraltıyordu. | Kısa yatay ekranlarda kişi listesi ve konuşma yan yana yerleştirildi. |
| Uzun bağlantılar mesaj balonunu genişletip içeride kesilebiliyordu. | Mesaj ızgarası ve balonların minimum genişliği düzeltildi; uzun bağlantı ve dosya adları satıra bölünüyor. |
| Haritanın gecikmeli boyutlandırma işlemleri ekran kapandıktan sonra çalışabiliyordu. | Bekleyen işlemler kapanışta temizleniyor; art arda boyut değişiklikleri birleştiriliyor. |

## Doğrulama sonuçları

- Ana ekranlar, detaylar, menüler ve pencerelerde **157 durum** incelendi; bu turda sayfa dışına yatay taşma veya JavaScript çalışma hatası bulunmadı.
- Müdür/admin için etkinlik, harita ve uzun mesaj verileriyle **60 durum** kontrol edildi; son turda taşma veya JavaScript çalışma hatası bulunmadı.
- Dikey/yatay telefon ve tabletlerde **14 stok/zimmet pencere durumu** kontrol edildi: pencereler ekran içinde kaldı ve kaydetme düğmeleri kaydırılarak erişilebildi.
- Hesap penceresi ve 1024 px tablet sorunlarından sonra **31 hedefli durum** yeniden kontrol edildi: küçük giriş yazısı, normal ekranlarda 32 px altında kalan düğme veya sayfa dışına taşma bulunmadı.
- Uzun mesaj/bağlantı ve kısa yatay konuşma yerleşimi için **12 ek durum** kontrol edildi: balon içinde gizli yatay taşma yok; 844 × 390 görünümünde konuşma kaydırma alanı 177 px yüksekliğinde.
- Giriş ekranı dört telefon/tablet genişliğinde, şifre göster/gizle düğmesiyle birlikte kontrol edildi.
- Menüde Tab odağı, Escape ile kapatma, odağın geri dönüşü ve 820 → 1024 → 820 boyut değişiminde kaydırmanın serbest bırakılması doğrulandı.
- Personel formlarında yeni değer yazma ve mevcut unvan/görev seçme istekleri korundu; alan yükseklikleri ve aynı satırdaki hizalar kontrol edildi.
- Ön yüz derlemesi ve mevcut 59 birim testi başarılı.

Organizasyon şeması geniş içeriği nedeniyle telefonlarda yakınlaştırma ve sürüklemeyle incelenir.
