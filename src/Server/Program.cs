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

    if (connectedClients.Any(c => c.Key.Nickname == nickname && c.Key.Room == room))
    {
        Console.WriteLine($"Error: Nickname \"{nickname}\" already exists in room: {room}");
        context.Response.StatusCode = (int)HttpStatusCode.Conflict;
        return;
    }

    using var ws = await context.WebSockets.AcceptWebSocketAsync();

    var client = new Client { Nickname = nickname, Room = room };
    connectedClients.TryAdd(client, ws);

    //Console.WriteLine($"*SERVER -> {nickname} connected to {room}");

    await HandleWebSocketMessages(client, ws);
});

app.MapGet("/clients", () =>
{
    return Results.Ok(connectedClients.Keys);
});

app.MapGet("/clients/{room}", (string room) =>
{
    return Results.Ok(connectedClients.Keys.Where(c => c.Room == room));
});

app.Run();

async Task HandleWebSocketMessages(Client client, WebSocket webSocket)
{
    var buffer = new byte[1024 * 4];
    var server = new Client { Nickname = "SERVER", Room = client.Room };

    try
    {
        string joinMessage = $"{client.Nickname} joined {client.Room}";
        var serverMsg = new Message
        {
            Type = MessageType.Message,
            Content = joinMessage,
            From = server,
            To = client.Room
        };

        foreach (var connectedClient in connectedClients.Where(
                       c => c.Key.Room == client.Room && c.Value.State == WebSocketState.Open))
        {
            await connectedClient.Value.SendAsync(
                buffer: new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(serverMsg))),
                messageType: WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken: CancellationToken.None
            );
        }

        Console.WriteLine($"{FormatDateTime(serverMsg.SentAt)} || *SERVER -> {joinMessage}");

        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var buffStr = Encoding.UTF8.GetString(buffer, 0, result.Count);

                try
                {
                    var receivedMessage = JsonSerializer.Deserialize<Message>(buffStr)
                        ?? throw new Exception($"{client.Nickname} sent and invalid json message");

                    receivedMessage.From = client;
                    receivedMessage.To = client.Room;

                    Console.WriteLine($"{FormatDateTime(receivedMessage.SentAt)} || [{receivedMessage.To}] -> {receivedMessage.From.Nickname}: {receivedMessage.Content}");

                    var json = JsonSerializer.Serialize(receivedMessage);

                    foreach (var connectedClient in connectedClients.Where(
                        c => c.Key.Room == client.Room && c.Value.State == WebSocketState.Open))
                    {
                        await connectedClient.Value.SendAsync(
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
                connectedClients.TryRemove(client, out _);

                string disconnectMessage = $"{client.Nickname} disconnected from {client.Room}";
                var serverMessage = new Message
                {
                    Type = MessageType.Message,
                    Content = disconnectMessage,
                    From = server,
                    To = client.Room
                };

                foreach (var connectedClient in connectedClients.Where(
                    c => c.Key.Room == client.Room && c.Value.State == WebSocketState.Open))
                {
                    await connectedClient.Value.SendAsync(
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

                Console.WriteLine($"{FormatDateTime(serverMessage.SentAt)} || *SERVER -> {disconnectMessage}");
                break;
            }
        }
    }
    catch (WebSocketException wse)
    {
        Console.WriteLine($"Log (WebSocket) {nameof(HandleWebSocketMessages)} -> {wse.Message}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Log (Critical) {nameof(HandleWebSocketMessages)} -> {ex.Message}");
    }
}

string FormatDateTime(DateTime dateTime) => dateTime.ToString("dd/MM/yyyy - HH:mm:ss");