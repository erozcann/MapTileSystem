using Fleck;
using System.Collections.Generic;

var allSockets = new List<IWebSocketConnection>();
var server = new WebSocketServer("ws://0.0.0.0:5000");

server.Start(socket =>
{
    socket.OnOpen = () =>
    {
        Console.WriteLine($"[+] Bağlandı: {socket.ConnectionInfo.ClientIpAddress}");
        allSockets.Add(socket);
    };
    socket.OnClose = () =>
    {
        Console.WriteLine($"[-] Ayrıldı: {socket.ConnectionInfo.ClientIpAddress}");
        allSockets.Remove(socket);
    };
    socket.OnMessage = message =>
    {
        Console.WriteLine($"[>] Mesaj: {message}");
        foreach (var s in allSockets)
        {
            if (s.IsAvailable)
                s.Send(message);
        }
    };
});

Console.WriteLine("Progress Notifier WebSocket sunucusu başlatıldı (ws://0.0.0.0:5000)");
Console.WriteLine("Çıkmak için bir tuşa basın...");
Console.ReadKey(); 