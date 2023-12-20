using Shared.Enums;
using Shared.Models;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

public class WebSocketHandler
{
    private readonly ConcurrentDictionary<Client, WebSocket> _connectedClients;

    public WebSocketHandler(ConcurrentDictionary<Client, WebSocket> connectedClients)
    {
        _connectedClients = connectedClients;
    }

    public async Task HandleWebSocketAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        var nickname = context.Request.Query["nickname"].ToString();
        var room = context.Request.Query["room"].ToString();

        if (string.IsNullOrWhiteSpace(nickname) || string.IsNullOrWhiteSpace(room))
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotAcceptable;
            return;
        }

        var client = new Client { Nickname = nickname, Room = room };

        if (_connectedClients.Any(c => c.Key.Nickname == nickname && c.Key.Room == room))
        {
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            return;
        }

        using var ws = await context.WebSockets.AcceptWebSocketAsync();

        _connectedClients.TryAdd(client, ws);

        await HandleWebSocketMessages(client, ws);
    }

    private async Task HandleWebSocketMessages(Client client, WebSocket webSocket)
    {
        var buffer = new byte[1024 * 4];

        try
        {
            HandleClientConnect(client);

            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    ProcessTextMessage(Encoding.UTF8.GetString(buffer, 0, result.Count), client);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    HandleClientDisconnect(client);
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

    private void BroadcastMessage(Message message)
    {
        foreach (var connectedClient in _connectedClients
            .Where(c => c.Key.Room == message.To && c.Value.State == WebSocketState.Open))
        {
            SendMessage(connectedClient.Value, message);
        }
    }

    private void ProcessTextMessage(string messageStr, Client client)
    {
        try
        {
            var receivedMessage = JsonSerializer.Deserialize<Message>(messageStr)
                ?? throw new Exception($"{client.Nickname} enviou uma mensagem JSON inválida");

            receivedMessage.From = client;
            receivedMessage.To = client.Room;

            Console.WriteLine($"{FormatDateTime(receivedMessage.SentAt)} || [{receivedMessage.To}] -> {receivedMessage.From.Nickname}: {receivedMessage.Content}");

            BroadcastMessage(receivedMessage);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private void HandleClientConnect(Client client)
    {
        string joinMessage = $"{client.Nickname} entrou na sala {client.Room}";
        var serverMsg = new Message
        {
            Type = MessageType.Message,
            Content = joinMessage,
            From = new Client { Nickname = "SERVER", Room = client.Room },
            To = client.Room
        };

        Console.WriteLine($"{FormatDateTime(serverMsg.SentAt)} || *SERVER -> {serverMsg.Content}");

        BroadcastMessage(serverMsg);

    }

    private void HandleClientDisconnect(Client client)
    {
        _connectedClients.TryRemove(client, out _);

        string disconnectMessage = $"{client.Nickname} desconectou-se da sala {client.Room}";
        var serverMessage = new Message
        {
            Type = MessageType.Message,
            Content = disconnectMessage,
            From = new Client { Nickname = "SERVER", Room = client.Room },
            To = client.Room
        };

        Console.WriteLine($"{FormatDateTime(serverMessage.SentAt)} || *SERVER -> {serverMessage.Content}");

        BroadcastMessage(serverMessage);
    }

    private void SendMessage(WebSocket webSocket, Message message)
    {
        var json = JsonSerializer.Serialize(message);
        webSocket.SendAsync(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(json)),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None);
    }

    private string FormatDateTime(DateTime dateTime) => dateTime.ToString("dd/MM/yyyy - HH:mm:ss");
}

class Program
{
    static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();

        app.UseWebSockets();

        var connectedClients = new ConcurrentDictionary<Client, WebSocket>();
        var webSocketHandler = new WebSocketHandler(connectedClients);

        app.MapGet("/", webSocketHandler.HandleWebSocketAsync);

        app.MapGet("/clients", () =>
        {
            return Results.Ok(connectedClients.Keys);
        });

        app.MapGet("/clients/{room}", (string room) =>
        {
            return Results.Ok(connectedClients.Keys.Where(c => c.Room == room));
        });

        app.Run();
    }
}
