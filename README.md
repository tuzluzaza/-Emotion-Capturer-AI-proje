====================================================================
                 (Emotion Capturer AI)
YAPAY ZEKA DESTEKLİ BİNA ERİŞİM VE DUYGU KONTROL SİSTEMİ 
====================================================================

Bu proje, bir bina veya tesise giriş yapmak isteyen kişilerin yüzlerindeki duygu durumunu analiz ederek, kişinin sahip olduğu role ve o anki duygu durumuna göre belirli odalara giriş yetkisi veren akıllı bir erişim kontrol sistemidir. Ayrıca sistem logları Gemini AI ile analiz edilerek yöneticilere güvenlik ve kullanım raporları sunulmaktadır.

Proje iki ana bileşenden oluşmaktadır:
1. Backend (Python - FastAPI & DeepFace)
2. Frontend Mobil Uygulama (.NET MAUI - C#)

====================================================================
GEREKSİNİMLER VE KURULUM ÖNCESİ HAZIRLIKLAR
====================================================================

[Backend İçin Gerekenler]
- Python 3.9 veya daha güncel bir sürüm (Projeyi çalıştırırken Python'un sistem PATH'ine ekli olduğundan emin olun).
- Gerekli Python kütüphaneleri (kurulum adımında anlatılmaktadır).
- Ücretsiz bir Gemini API Anahtarı (Log analiz modülü için gereklidir. https://aistudio.google.com adresinden alınabilir).

[Frontend (Mobil Uygulama) İçin Gerekenler]
- Visual Studio 2022 (Tercihen en güncel sürüm).
- ".NET Multi-platform App UI development" (.NET MAUI) iş yükünün Visual Studio'da kurulu olması.
- .NET 9.0 SDK.
- Android Emulator veya USB Hata Ayıklama (USB Debugging) açık gerçek bir Android cihaz.

====================================================================
KURULUM ADIMLARI
====================================================================

---------------------------------------------------
A. BACKEND (SUNUCU) KURULUMU
---------------------------------------------------
1. Proje klasöründeki "backend" dizinine gidin.
   Örn: cd backend

2. Gerekli Python kütüphanelerini kurun. Komut satırına (CMD/Terminal) şu komutu yazın:
   pip install -r requirements.txt
   
   (Bu komut FastAPI, Uvicorn, DeepFace, OpenCV, TensorFlow, Google Generative AI gibi gerekli tüm kütüphaneleri indirecektir).

3. Konfigürasyon Dosyasını Ayarlayın:
   "backend" klasörü içinde "config.py" adında bir dosya oluşturun veya varsa düzenleyin. İçerisine aldığınız Gemini API anahtarını şu formatta ekleyin:
   GEMINI_API_KEY = "SİZİN_API_ANAHTARINIZ_BURAYA_GELECEK"

4. Sunucuyu Başlatın:
   Yine "backend" dizinindeyken komut satırına şu komutu yazarak sunucuyu başlatın:
   python main.py

   (Not: İlk çalıştırmada DeepFace kütüphanesi yüz tanıma modellerini indireceği için biraz zaman alabilir).
   Sunucu başarıyla başladığında "Uvicorn running on http://0.0.0.0:8000" şeklinde bir mesaj göreceksiniz.

---------------------------------------------------
B. FRONTEND (MOBİL UYGULAMA) KURULUMU
---------------------------------------------------
1. Proje ana klasöründeki "frontend\FaceEmotionApp" dizinine gidin.
2. "FaceEmotionApp.sln" (veya .csproj) dosyasını Visual Studio 2022 ile açın.
3. API Bağlantı Ayarı (Önemli!):
   Eğer uygulamayı fiziksel telefonunuzda veya bir emülatörde deneyecekseniz, uygulamanın bilgisayarınızdaki backend'e ulaşabilmesi için IP adresinizi ayarlamanız gerekir.
   - Bilgisayarınızın yerel IP adresini öğrenin (CMD'ye "ipconfig" yazarak IPv4 Adresini kopyalayın, örn: 192.168.1.50).
   - Çözüm Gezgini'nden (Solution Explorer) "Services/ApiService.cs" dosyasını açın.
   - "_baseUrl" değişkenini bulun ve bilgisayarınızın IP adresini yazın:
     Örn: private readonly string _baseUrl = "http://192.168.1.50:8000";

4. Derleme ve Çalıştırma:
   Visual Studio'nun üst panelinden hedef cihazı (Android Emulator veya bilgisayarınıza bağlı telefon) seçin ve yeşil "Play" (Çalıştır) butonuna basarak projeyi derleyip çalıştırın.

====================================================================
SİSTEMİN TEMEL ÖZELLİKLERİ VE KULLANIMI
====================================================================

- Yüz ve Duygu Tanıma: Uygulama kameradan fotoğraf çeker ve backend'e gönderir. DeepFace kütüphanesi kişinin yüzünü algılar ve baskın duygu durumunu (Mutlu, Üzgün, Kızgın, Nötr vb.) tespit eder.
- Dinamik Oda ve Rol Yönetimi: Yöneticiler (Müdür vb.), yönetim paneli üzerinden yeni odalar oluşturabilir, yeni roller ekleyebilir, hangi rollerin hangi odalara erişebileceğini ve o odaya girmek için hangi duygu durumunda olunması gerektiğini belirleyebilir.
- Yapay Zeka Log Analizi: Sistemdeki tüm giriş çıkış denemeleri, rol değişimleri ve yetkisiz erişim denemeleri loglanır. "Yönetim Paneli > AI Log Analizi" sekmesinden bu loglar Gemini AI kullanılarak analiz edilir ve yöneticiye bir güvenlik/özet raporu sunulur.

İyi çalışmalar!
