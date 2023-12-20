using Shared.Models;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseWebSockets();

var connectedClients = new ConcurrentDictionary<string, WebSocket>();

app.MapGet("/", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        Console.WriteLine("Error: No WebSocket request");
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        return;
    }

    var name = context.Request.Query["nickname"].ToString();

    if (string.IsNullOrWhiteSpace(name))
    {
        Console.WriteLine("Error: No nickname provided");
        context.Response.StatusCode = (int)HttpStatusCode.NotAcceptable;
        return;
    }

    using var ws = await context.WebSockets.AcceptWebSocketAsync();

    if (!connectedClients.TryAdd(name, ws))
    {
        Console.WriteLine($"Error: Nickname \"{name}\" already exists");

        await ws.CloseAsync(
            closeStatus: WebSocketCloseStatus.NormalClosure,
            statusDescription: "Nickname already exists",
            cancellationToken: CancellationToken.None
        );

        return;
    }

    Console.WriteLine($"*SERVER -> {name} connected");

    await HandleWebSocketMessages(ws, name);
});

app.Run();

async Task HandleWebSocketMessages(WebSocket webSocket, string name)
{
    var buffer = new byte[1024 * 4];

    while (webSocket.State == WebSocketState.Open)
    {
        var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        if (result.MessageType == WebSocketMessageType.Text)
        {
            var buffStr = Encoding.UTF8.GetString(buffer, 0, result.Count);

            try
            {
                // parse the message to a Message object
                var msgObj = JsonSerializer.Deserialize<Message>(buffStr)
                    ?? throw new Exception($"{name} tried to send an invalid message format");

                // Console.WriteLine($"{name}: {message}");
                Console.WriteLine($"[{msgObj.Room}] -> {name}: {msgObj.Content}");

                var msgJson = JsonSerializer.Serialize(msgObj);

                foreach (var client in connectedClients)
                {
                    var connection = client.Value;

                    if (connection.State != WebSocketState.Open)
                    {
                        continue;
                    }

                    await connection.SendAsync(
                        //buffer: new ArraySegment<byte>(buffer, 0, result.Count),
                        buffer: new ArraySegment<byte>(Encoding.UTF8.GetBytes(msgJson)),
                        messageType: result.MessageType,
                        endOfMessage: true,
                        cancellationToken: CancellationToken.None
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        else if (result.MessageType == WebSocketMessageType.Close)
        {
            connectedClients.TryRemove(name, out _);

            await webSocket.CloseAsync(
                closeStatus: WebSocketCloseStatus.NormalClosure,
                statusDescription: null,
                cancellationToken: CancellationToken.None
            );

            Console.WriteLine($"*SERVER -> {name} disconnected");
            break;
        }
    }
}