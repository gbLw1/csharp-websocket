using Shared.Enums;
using Shared.Models;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseWebSockets();

var connectedClients = new ConcurrentDictionary<Client, WebSocket>();

app.MapGet("/", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        Console.WriteLine("Error: No WebSocket request");
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        return;
    }

    var nickname = context.Request.Query["nickname"].ToString();

    if (string.IsNullOrWhiteSpace(nickname))
    {
        Console.WriteLine("Error: No nickname provided");
        context.Response.StatusCode = (int)HttpStatusCode.NotAcceptable;
        return;
    }

    var room = context.Request.Query["room"].ToString();

    if (string.IsNullOrWhiteSpace(room))
    {
        Console.WriteLine("Error: No room provided");
        context.Response.StatusCode = (int)HttpStatusCode.NotAcceptable;
        return;
    }

    using var ws = await context.WebSockets.AcceptWebSocketAsync();

    if (!connectedClients.TryAdd(new() { Nickname = nickname, Room = room }, ws))
    {
        Console.WriteLine($"Error: Nickname \"{nickname}\" already exists");

        try
        {
            await ws.CloseAsync(
                closeStatus: WebSocketCloseStatus.NormalClosure,
                statusDescription: $"Error: Nickname \"{nickname}\" already exists",
                cancellationToken: CancellationToken.None
            );
        }
        catch (WebSocketException wse)
        {
            Console.WriteLine($"Log (WebSocket) -> While closing connection: {wse.Message}");
        }

        return;
    }

    Console.WriteLine($"*SERVER -> {nickname} connected to {room}");

    await HandleWebSocketMessages(ws, nickname, room);
});

app.Run();

async Task HandleWebSocketMessages(WebSocket webSocket, string nickname, string room)
{
    var buffer = new byte[1024 * 4];

    try
    {
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var buffStr = Encoding.UTF8.GetString(buffer, 0, result.Count);

                try
                {
                    var receivedMessage = JsonSerializer.Deserialize<Message>(buffStr)
                        ?? throw new Exception($"{nickname} sent and invalid json message");

                    receivedMessage.From = nickname;
                    receivedMessage.To = room;
                    Console.WriteLine($"[{receivedMessage.To}] -> {receivedMessage.From}: {receivedMessage.Content}");

                    var json = JsonSerializer.Serialize(receivedMessage);

                    foreach (var client in connectedClients.Where(c => c.Key.Room == room && c.Value.State == WebSocketState.Open))
                    {
                        await client.Value.SendAsync(
                            buffer: new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)),
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
                var disconnectedClient = connectedClients.FirstOrDefault(x => x.Key.Nickname == nickname && x.Key.Room == room).Key;
                connectedClients.TryRemove(disconnectedClient, out _);

                string disconnectMessage = $"{nickname} disconnected from {room}";
                var serverMessage = new Message
                {
                    Type = MessageType.Message,
                    Content = disconnectMessage,
                    From = "SERVER",
                    To = room
                };

                foreach (var user in connectedClients.Where(c => c.Key.Room == room && c.Value.State == WebSocketState.Open))
                {
                    await user.Value.SendAsync(
                        buffer: new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(serverMessage))),
                        messageType: WebSocketMessageType.Text,
                        endOfMessage: true,
                        cancellationToken: CancellationToken.None
                    );
                }

                await webSocket.CloseAsync(
                    closeStatus: WebSocketCloseStatus.NormalClosure,
                    statusDescription: $"*SERVER -> {disconnectMessage}",
                    cancellationToken: CancellationToken.None
                );

                Console.WriteLine($"*SERVER -> {disconnectMessage}");
                break;
            }
        }
    }
    catch (WebSocketException wse)
    {
        Console.WriteLine($"Log (WebSocket) -> {wse.Message}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Log (Critical) -> {ex.Message}");
    }
}

// TODO: Broadcast messages
//async Task Broadcast(string message, string room)
//{
//    var json = JsonSerializer.Serialize(new Message
//    {
//        Type = MessageType.Message,
//        Content = message,
//        From = "SERVER",
//        To = room
//    });

//    foreach (var client in connectedClients.Where(c => c.Key.Room == room && c.Value.State == WebSocketState.Open))
//    {
//        await client.Value.SendAsync(
//            buffer: new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)),
//            messageType: WebSocketMessageType.Text,
//            endOfMessage: true,
//            cancellationToken: CancellationToken.None
//        );
//    }
//}