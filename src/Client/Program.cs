using Shared.Models;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

class Program
{
    static async Task Main(string[] args)
    {
        await RunWebSocketClientAsync();
    }

    static async Task RunWebSocketClientAsync()
    {
        string baseUri = "ws://localhost:5100/";
        using var ws = new ClientWebSocket();

        try
        {
            Console.WriteLine("Welcome to WebSocket server...");

            Console.Write("Enter your nickname: ");
            var nickname = Console.ReadLine();

            await ws.ConnectAsync(new Uri($"{baseUri}?nickname={nickname}"), CancellationToken.None);

            _ = Task.Run(async () => await ReceiveMessages(ws));

            Console.WriteLine("Checking your credentials...");

            await Task.Delay(2000); // Simulate a delay

            Console.WriteLine("Connected to WebSocket server. Type ':q!' to close the connection.");

            string message = string.Empty;
            while (message?.ToLower() != ":q!")
            {
                if (ws.State != WebSocketState.Open)
                {
                    break;
                }

                await Task.Delay(100);

                Console.Write("Enter message: ");
                message = Console.ReadLine() ?? string.Empty;

                if (message?.ToLower() != ":q!")
                {
                    await SendMessage(ws, nickname, message);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            if (ws.State == WebSocketState.Open)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed", CancellationToken.None);
            }
        }
    }

    static async Task SendMessage(ClientWebSocket ws, string? from, string? message)
    {
        var json = JsonSerializer.Serialize(new Message
        {
            From = from ?? "Anonymous",
            Content = message ?? string.Empty,
            Room = "general",
        });

        await ws.SendAsync(
            buffer: new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)),
            messageType: WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken: CancellationToken.None
        );
    }

    static async Task ReceiveMessages(ClientWebSocket ws)
    {
        var buffer = new byte[1024 * 4];

        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                Console.WriteLine($"Error: {result.CloseStatusDescription}");
                break;
            }
            else
            {
                var receivedMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);

                try
                {
                    var msgObj = JsonSerializer.Deserialize<Message>(receivedMessage)
                        ?? throw new Exception("Received an invalid message format");

                    Console.WriteLine($"[{msgObj.Room}] -> {msgObj.From}: {msgObj.Content}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }
    }
}
