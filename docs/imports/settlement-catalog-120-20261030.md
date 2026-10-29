# Kullanıcının 120 mahalle kataloğu — 30 Ekim 2026

30 Ekim mesajındaki 120 mahalle/muhtar çifti, mevcut `sehitkamil-headmen-20261029.json` kaynağıyla sıra ve metin dahil birebir karşılaştırıldı; 120 çiftin tamamı aynı.

Hatanın nedeni: ham GeoJSON kaynağı 149 alan içeriyordu; bunların 29'u kullanıcının güncel listesinde bulunmuyordu. Önceki ad/muhtar güncellemesi yalnızca 120 kaydı düzeltmiş, 29 eski alanı aktif listelerde bırakmıştı.

Yeni tek seferlik işlem:

- Kullanıcının 120 kaydını resmi kodları üzerinden etkinleştirir, ad ve muhtar bilgilerini tam metinle uygular.
- Ayrı kaynakta belirtilen 29 eski kodu pasif yapar; kayıtları, geometrileri, telefonları, nüfus ve etkinlik bağlantılarını silmez.
- Harita özeti, arama, kartlar ve etkinlik formunun mahalle seçimi mevcut aktif kayıt süzgeciyle 120 kaydı döndürür.
- Harita, eski poligonları yalnızca nötr ve seçilemeyen bağlam olarak tutar; kullanıcı listesindeki 120 mahalleye kalıcı ad etiketi gösterir. GeoJSON sınırları değişmez.
- Tam kod eşleşmesi ve kayıt sonrası 120 ad/muhtar doğrulaması tek veritabanı işlemi içindedir. İşlem makbuzu sonraki kullanıcı düzenlemelerinin yeniden başlatmada ezilmesini engeller.

Doğrulama: API listesi ve harita özetinde 120 tam ad/muhtar çifti; 149 geometri ve geçmiş bağlantıların korunması; eksik referansta işlemin uygulanmaması; tekrar başlatmada kullanıcı değişikliklerinin korunması. Kontrollü tarayıcı testinde 1440, 390 ve 820 piksel ekranlarda 120 görünür etiket, yakınlaştırma/uzaklaştırma/sıfırlama, 120 açılır liste seçeneği ve muhtar adıyla arama kontrol edilir.
