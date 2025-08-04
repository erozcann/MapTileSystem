🗺️ Map Tile Downloader & Manager
Bu proje, offline harita verilerinin kullanıcı tarafından seçilen alanlara göre indirilmesini ve yönetilmesini sağlayan çok katmanlı bir sistemdir. Proje dört farklı uygulamadan oluşur: Desktop Downloader, Web Dashboard, REST API ve WebSocket tabanlı Middleware.

🚀 Proje Bileşenleri
1. 🖥️ Desktop App - Map Tile Downloader (WPF)
Kullanıcıların harita üzerinde dikdörtgen çizerek alan seçtiği ve belirtilen zoom seviyeleri için OpenStreetMap tile görsellerini indirdiği WPF uygulamasıdır.

Alan seçimi (WebView2 + Leaflet)

Zoom seviyesi belirleme (min-max)

Dosya sistemine .png uzantılı olarak klasör halinde görsel kaydetme

SQLite'a meta veri (isim, koordinat, zoom, dosya yolu) kaydı

WebSocket üzerinden anlık indirme ilerleme bildirimi

2. 🌐 Web Dashboard - Map Tile Manager (Blazor Server)
Kullanıcıların indirilmiş haritaları görüntülediği, yönettiği ve indirme durumlarını takip ettiği arayüzdür.

JWT ile kullanıcı girişi

API üzerinden harita listeleme, silme, yeniden adlandırma

İndirme sırasında anlık % durumu gösterimi (WebSocket)

Leaflet ile offline harita görüntüleme

3. 🔌 REST API - Map Tile API (.NET Minimal API)
SQLite veri tabanındaki data ve users tablolarına erişim sağlayan RESTful API'dir.

Giriş ve kayıt işlemleri (JWT Authentication)

Harita listeleme, silme, güncelleme (meta verilerle birlikte)

Silinen kayıtların dosya sisteminden de temizlenmesi

4. 📡 Middleware - Progress Notifier (Console App)
Desktop uygulaması ile Web Dashboard arasında indirme ilerleme bilgisini gerçek zamanlı olarak aktaran WebSocket sunucusudur.

WebSocket protokolü ile % bilgisi yayınlama

Birden fazla istemcinin aynı anda bağlanabilmesi

Bağlı tüm istemcilere anlık push mesajları gönderme

🛠️ Kullanılan Teknolojiler
Katman	Teknoloji
Desktop	WPF (.NET C#)
Web Dashboard	Blazor Server (SPA)
REST API	.NET 8 Minimal API
Middleware	Console App + WebSocket
Harita Servisi	OpenStreetMap + Leaflet.js
Veritabanı	SQLite
Kimlik Doğrulama	JWT

📂 Proje Yapısı
MapTileDownloader // WPF Desktop App
MapTileAPI // RESTful API
MapTileManager // Blazor Web Dashboard
ProgressNotifier // Console WebSocket Server
Shared/Database.db // Ortak kullanılan SQLite veritabanı

🔐 Kullanıcı Girişi Bilgileri
Dashboard için:

Kullanıcı Adı: pavotek

Parola: STAJ2025@PAVOTEK.COM (hashlenmiş halde SQLite veritabanında)

🧪 Çalıştırma Adımları
1. Veritabanı Oluşturun
SQLite veritabanınızda users ve data tablolarını aşağıdaki gibi oluşturun:

sql
Kopyala
Düzenle
CREATE TABLE users (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  username TEXT,
  password TEXT
);

CREATE TABLE data (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT,
  created_at TEXT,
  zoom_min INTEGER,
  zoom_max INTEGER,
  top_left_lat REAL,
  top_left_lng REAL,
  bottom_right_lat REAL,
  bottom_right_lng REAL,
  path TEXT
);
2. Uygulamaları Sırayla Başlatın
ProgressNotifier (WebSocket sunucu): ws://localhost:8181

MapTileAPI (API sunucu): https://localhost:7061

MapTileDownloader (WPF)

MapTileManager (Blazor)

📦 Haritalar Nerede Saklanır?
Her harita klasörü şu formatta adlandırılır:
HaritaAdı_Tarih_Saat → Örn: Ankara_Golbek_2025_07_11__14_36

Görseller X/Y/Z klasör yapısıyla .png olarak kaydedilir.

Meta veriler sadece SQLite içindeki data tablosuna yazılır.

📡 WebSocket İletişimi
Gönderilen örnek mesaj:

json
Kopyala
Düzenle
{
  "mapName": "Ankara_Golbek",
  "percent": 78.2
}
📷 Harita Önizleme (Blazor’da)
map.html dosyası WebView veya iframe içinde şu şekilde açılır:

bash
Kopyala
Düzenle
http://localhost:7061/map.html?folder=Ankara_Golbek_2025_07_11__14_36
👥 Geliştirici
Bu proje Pavotek bünyesinde, Web/UI departmanı tarafından 2025 yaz stajı kapsamında geliştirilmiştir.