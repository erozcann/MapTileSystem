using Microsoft.Web.WebView2.Wpf;
using System;
using System.IO;
using System.Windows;
using System.Text.Json;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Websocket.Client;
using System.Reactive.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;



namespace MapTileDownloader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            CalculateTilesButton.Click += CalculateTilesButton_Click;
            StartDownloadButton.Click += StartDownloadButton_Click;
            OpenFolderButton.Click += OpenFolderButton_Click;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Zoom seviyelerini yükle
            for (int i = 1; i <= 20; i++)
            {
                MinZoomComboBox.Items.Add(i);
            }

            // Min seçilince Max'leri filtrele
            MinZoomComboBox.SelectionChanged += (s, ev) =>
            {
                if (MinZoomComboBox.SelectedItem is int selectedMin)
                {
                    MaxZoomComboBox.Items.Clear();
                    for (int i = selectedMin + 1; i <= 20; i++)
                    {
                        MaxZoomComboBox.Items.Add(i);
                    }
                    if (MaxZoomComboBox.Items.Count > 0)
                        MaxZoomComboBox.SelectedIndex = 0;
                }
            };

            // Başlangıçta varsayılan seçim
            MinZoomComboBox.SelectedIndex = 4; // 5
                                               // MaxZoom Combo min seçilince dolacağı için burada bırakıyoruz

            // Harita yükle
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "map.html");
            if (!File.Exists(htmlPath))
            {
                File.WriteAllText(htmlPath, MapHtmlContent());
            }

            await MapWebView.EnsureCoreWebView2Async();
            MapWebView.Source = new Uri(htmlPath);
        }



        private async void CalculateTilesButton_Click(object sender, RoutedEventArgs e)
        {
            // MinZoomTextBox ve MaxZoomTextBox geçen yerleri MinZoomComboBox ve MaxZoomComboBox ile değiştir
            // Zoom değerlerini ComboBox üzerinden al
            if (MinZoomComboBox.SelectedItem == null || MaxZoomComboBox.SelectedItem == null)
            {
                MessageBox.Show("Zoom seviyeleri seçilmelidir.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            int minZoom = (int)MinZoomComboBox.SelectedItem;
            int maxZoom = (int)MaxZoomComboBox.SelectedItem;
            if (minZoom < 1 || minZoom > 20 || maxZoom < 1 || maxZoom > 20)
            {
                MessageBox.Show("Zoom seviyeleri 1 ile 20 arasında olmalıdır.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (maxZoom < minZoom)
            {
                MessageBox.Show("Maksimum zoom, minimum zoom'dan küçük olamaz.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            // Harita üzerinde alan seçili mi kontrol et
            string coordsJson = await MapWebView.ExecuteScriptAsync("window.getRectangleCoords && window.getRectangleCoords();");
            if (string.IsNullOrWhiteSpace(coordsJson) || coordsJson == "null")
            {
                MessageBox.Show("Lütfen harita üzerinde iki nokta seçerek alan belirleyin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // JSON'dan koordinatları al (çift tırnak içindeki string'i çöz)
            coordsJson = JsonDocument.Parse(coordsJson).RootElement.GetString();
            var coords = JsonSerializer.Deserialize<RectangleCoords>(coordsJson);

            // Tile sayısını hesapla
            int totalTiles = CalculateTileCount(coords, minZoom, maxZoom);
            TileCountTextBlock.Text = $"Toplam Tile: {totalTiles}";
        }

        private int CalculateTileCount(RectangleCoords coords, int minZoom, int maxZoom)
        {
            int total = 0;
            for (int z = minZoom; z <= maxZoom; z++)
            {
                var (xMin, yMin) = LatLonToTile(coords.p1.lat, coords.p1.lng, z);
                var (xMax, yMax) = LatLonToTile(coords.p2.lat, coords.p2.lng, z);

                int maxTileIndex = (1 << z) - 1;
                int x1 = Math.Max(0, Math.Min(xMin, xMax));
                int x2 = Math.Min(maxTileIndex, Math.Max(xMin, xMax));
                int y1 = Math.Max(0, Math.Min(yMin, yMax));
                int y2 = Math.Min(maxTileIndex, Math.Max(yMin, yMax));

                total += (x2 - x1 + 1) * (y2 - y1 + 1);
            }
            return total;
        }


        private (int x, int y) LatLonToTile(double lat, double lon, int zoom)
        {
            double latRad = lat * Math.PI / 180.0;
            int n = 1 << zoom;
            int x = (int)((lon + 180.0) / 360.0 * n);
            int y = (int)((1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n);
            Console.WriteLine($"LatLonToTile input: lat={lat}, lon={lon}, zoom={zoom} -> x={x}, y={y}");
            return (x, y);
        }

        private class RectangleCoords
        {
            public PointLatLng p1 { get; set; }
            public PointLatLng p2 { get; set; }
        }
        private class PointLatLng
        {
            public double lat { get; set; }
            public double lng { get; set; }
        }

        private async void StartDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (MinZoomComboBox.SelectedItem == null || MaxZoomComboBox.SelectedItem == null)
            {
                MessageBox.Show("Zoom seviyeleri seçilmelidir.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            int minZoom = (int)MinZoomComboBox.SelectedItem;
            int maxZoom = (int)MaxZoomComboBox.SelectedItem;
            if (minZoom < 1 || minZoom > 20 || maxZoom < 1 || maxZoom > 20)
            {
                MessageBox.Show("Zoom seviyeleri 1 ile 20 arasında olmalıdır.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (maxZoom < minZoom)
            {
                MessageBox.Show("Maksimum zoom, minimum zoom'dan küçük olamaz.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            string coordsJson = await MapWebView.ExecuteScriptAsync("window.getRectangleCoords && window.getRectangleCoords();");
            if (string.IsNullOrWhiteSpace(coordsJson) || coordsJson == "null")
            {
                MessageBox.Show("Lütfen harita üzerinde iki nokta seçerek alan belirleyin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            coordsJson = JsonDocument.Parse(coordsJson).RootElement.GetString();
            var coords = JsonSerializer.Deserialize<RectangleCoords>(coordsJson);

            int totalTiles = CalculateTileCount(coords, minZoom, maxZoom);
            if (totalTiles == 0)
            {
                MessageBox.Show("Seçilen alan için indirilecek tile bulunamadı.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string mapName = MapNameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(mapName))
            {
                MessageBox.Show("Lütfen harita için bir isim girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string folderName = $"{mapName}_{DateTime.Now:yyyy_MM_dd__HH_mm}";
            string baseFolder = Path.Combine(@"C:\Users\Admin\source\repos\MapTileManager\MapTileManager\wwwroot\tiles", folderName);

            Directory.CreateDirectory(baseFolder);

            StartDownloadButton.IsEnabled = false;
            CalculateTilesButton.IsEnabled = false;
            DownloadProgressBar.Value = 0;
            DownloadProgressBar.Maximum = totalTiles;
            ProgressTextBlock.Text = $"İndiriliyor: 0/{totalTiles}";
            OpenFolderButton.Visibility = Visibility.Collapsed;

            var notifierUrl = new Uri("ws://localhost:8181/ws");
            using (var ws = new ClientWebSocket())
            {
                await ws.ConnectAsync(notifierUrl, CancellationToken.None);
                int downloaded = 0;
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("MapTileDownloader/1.0 (https://github.com/erenalp)");
                    for (int z = minZoom; z <= maxZoom; z++)
                    {
                        var (xMin, yMin) = LatLonToTile(coords.p1.lat, coords.p1.lng, z);
                        var (xMax, yMax) = LatLonToTile(coords.p2.lat, coords.p2.lng, z);

                        int maxTileIndex = (1 << z) - 1; // Tile index sınırı: 0 - (2^zoom-1)
                        int x1 = Math.Max(0, Math.Min(xMin, xMax));
                        int x2 = Math.Min(maxTileIndex, Math.Max(xMin, xMax));
                        int y1 = Math.Max(0, Math.Min(yMin, yMax));
                        int y2 = Math.Min(maxTileIndex, Math.Max(yMin, yMax));

                        for (int x = x1; x <= x2; x++)
                        {
                            for (int y = y1; y <= y2; y++)
                            {
                                string url = $"https://tile.openstreetmap.org/{z}/{x}/{y}.png";
                                Console.WriteLine($"Tile URL: {url}"); // DEBUG: indirdiğin tile adresi
                                string saveDir = Path.Combine(baseFolder, z.ToString(), x.ToString());
                                Directory.CreateDirectory(saveDir);
                                string savePath = Path.Combine(saveDir, $"{y}.png");
                                try
                                {
                                    var data = await client.GetByteArrayAsync(url);
                                    await File.WriteAllBytesAsync(savePath, data);
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"Tile indirilemedi:\n{url}\n\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                                }

                                downloaded++;
                                DownloadProgressBar.Value = downloaded;
                                ProgressTextBlock.Text = $"İndiriliyor: {downloaded}/{totalTiles}";

                                double percent = (double)downloaded / totalTiles * 100.0;
                                var progressMsg = new
                                {
                                    map = mapName,
                                    percent = percent,
                                    downloaded = downloaded,
                                    total = totalTiles
                                };
                                string json = System.Text.Json.JsonSerializer.Serialize(progressMsg);

                                Console.WriteLine("WS Progress Gönderiliyor: " + json);

                                var buffer = Encoding.UTF8.GetBytes(json);
                                await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                            }
                        }
                    }
                }
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
            }

            ProgressTextBlock.Text = "İndirme tamamlandı!";
            OpenFolderButton.Tag = baseFolder;
            OpenFolderButton.Visibility = Visibility.Visible;
            StartDownloadButton.IsEnabled = true;
            CalculateTilesButton.IsEnabled = true;

            SaveMetaToDatabase(mapName, DateTime.Now, coords, minZoom, maxZoom, baseFolder);
        }


        private void SaveMetaToDatabase(string mapName, DateTime date, RectangleCoords coords, int minZoom, int maxZoom, string folderPath)
        {
            string dbPath = @"C:\Users\Admin\source\repos\MapTileAPI\MapTileAPI\bin\Debug\net8.0\maptile.db";



            using (var conn = new SqliteConnection($"Data Source={dbPath}"))
            {
                conn.Open();

                var insert = @"INSERT INTO DATA (
            map_name, date, min_zoom, max_zoom, 
            coord1_lat, coord1_lng, coord2_lat, coord2_lng, 
            folder_path
        ) VALUES (
            @name, @date, @minz, @maxz, 
            @lat1, @lng1, @lat2, @lng2, 
            @folder
        );";

                using (var cmd = new SqliteCommand(insert, conn))
                {
                    cmd.Parameters.AddWithValue("@name", mapName);
                    cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@minz", minZoom);
                    cmd.Parameters.AddWithValue("@maxz", maxZoom);
                    cmd.Parameters.AddWithValue("@lat1", coords.p1.lat);
                    cmd.Parameters.AddWithValue("@lng1", coords.p1.lng);
                    cmd.Parameters.AddWithValue("@lat2", coords.p2.lat);
                    cmd.Parameters.AddWithValue("@lng2", coords.p2.lng);
                    cmd.Parameters.AddWithValue("@folder", folderPath);
                    cmd.ExecuteNonQuery();
                }
            }
        }


        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (OpenFolderButton.Tag is string folder && Directory.Exists(folder))
            {
                System.Diagnostics.Process.Start("explorer.exe", folder);
            }
        }

        private string MapHtmlContent()
        {
            return @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>Map</title>
    <meta name='viewport' content='width=device-width, initial-scale=1.0' />
    <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />
    <style>html, body, #map { height: 100%; margin: 0; padding: 0; } .rect { background: rgba(0, 128, 255, 0.2); border: 2px solid #0078ff; }</style>
</head>
<body>
    <div id='map' style='width:100%;height:100vh;'></div>
    <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
    <script>
        var map = L.map('map').setView([39.92, 32.85], 6);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '© OpenStreetMap'
        }).addTo(map);
        var markers = [];
        var rectangle = null;
        function updateRectangle() {
            if (rectangle) { map.removeLayer(rectangle); }
            if (markers.length == 2) {
                var bounds = L.latLngBounds(markers[0].getLatLng(), markers[1].getLatLng());
                rectangle = L.rectangle(bounds, {color: '#0078ff', weight: 2, fillOpacity: 0.2});
                rectangle.addTo(map);
            }
        }
        map.on('click', function(e) {
            if (markers.length < 2) {
                var marker = L.marker(e.latlng, {draggable:true}).addTo(map);
                marker.on('drag', updateRectangle);
                marker.on('dragend', updateRectangle);
                markers.push(marker);
                updateRectangle();
            }
        });
        window.clearMarkers = function() {
            markers.forEach(m => map.removeLayer(m));
            markers = [];
            if (rectangle) { map.removeLayer(rectangle); rectangle = null; }
        }
        window.getRectangleCoords = function() {
            if (markers.length == 2) {
                return JSON.stringify({
                    p1: markers[0].getLatLng(),
                    p2: markers[1].getLatLng()
                });
            }
            return null;
        }
    </script>
</body>
</html>";
        }
    }
}

