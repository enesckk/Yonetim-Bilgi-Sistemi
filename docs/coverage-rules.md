# Hizmetten yararlanma oranı

30 Ekim 2026 kullanıcı düzeltmesi: ayrı “Yok” kategorisi kullanılmaz.

- %25 altı kırmızı; etkinlik veya katılım girilmemiş mahalleler de kırmızı.
- %25 dahil, %50 hariç sarı.
- %50 ve üzeri yeşil.
- Nüfusu bilinen ve katılımı olmayan mahallede oran %0 hesaplanır. Harita özeti ve mahalle detayı aynı hesabı uygular.
- Nüfus bilinmiyorsa oran hesaplanamaz; gösterim “—”, sınıf yine kırmızıdır. Nüfus veya katılım verisi uydurulmaz.
- Kaynakta bildirilen yararlanıcı sayısı mevcutsa mevcut hesap onu, değilse toplam katılımı kullanır. Tekrarlı katılımlar için açıklama korunur.

Mahalle detayında oran kartı nüfusun altında, muhtar bilgisinin üzerinde yer alır. Oran, ilerleme çizgisi, üç renk aralığı ve etkinlik/katılım/nüfus değerleri birlikte gösterilir. Coğrafi sınırlar ve kullanıcı verileri değişmez.

Doğrulama: etkinliksiz, nüfuslu Sofalıcı örneğinde 378 nüfus / 0 katılım = %0 ve kırmızı; nüfussuz örnekte null oran ve kırmızı; %25 ve %50 sınırları için arka uç testleri. 1440, 390 ve 820 piksel tarayıcı kontrollerinde yalnızca üç renk grubu, 120 görünür etiket, taşmayan detay kartları ve %0/%25/%50 oranları doğrulanır.
