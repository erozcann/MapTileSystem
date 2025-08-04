using System.Net.WebSockets;
using System.Text;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseWebSockets();

// Bağlı WebSocket istemcilerini tutan thread-safe liste
var clients = new ConcurrentBag<WebSocket>();

app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        clients.Add(webSocket);
        Console.WriteLine("WebSocket bağlantısı alındı. Toplam istemci: " + clients.Count);

        var buffer = new byte[1024 * 4];
        try
        {
            while (true)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Kapatılıyor", CancellationToken.None);
                    break;
                }
                else
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine($"Gelen mesaj: {message}");
                    // Broadcast: Tüm bağlı istemcilere ilet
                    foreach (var client in clients)
                    {
                        if (client.State == WebSocketState.Open)
                        {
                            await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)), WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WebSocket hata: {ex.Message}");
        }
        finally
        {
            // Bağlantı kapandıysa, client listeden çıkarılamaz (ConcurrentBag), ama State kontrolü ile yönetilebilir.
            Console.WriteLine("WebSocket bağlantısı kapandı.");
        }
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

app.Run("http://localhost:8181");
