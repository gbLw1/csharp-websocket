using System.Net;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseWebSockets();

app.MapGet("/", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        return;
    }

    using var ws = await context.WebSockets.AcceptWebSocketAsync();

    Console.WriteLine("WebSocket connection established");

    await HandleWebSocketMessages(ws);
});

app.Run();

static async Task HandleWebSocketMessages(WebSocket webSocket)
{
    var buffer = new byte[1024 * 4];

    while (webSocket.State == WebSocketState.Open)
    {
        var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        if (result.MessageType == WebSocketMessageType.Text)
        {
            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            Console.WriteLine($"Received message from client: {message}");

            await webSocket.SendAsync(
                buffer: new ArraySegment<byte>(buffer, 0, result.Count),
                messageType: result.MessageType, result.EndOfMessage,
                cancellationToken: CancellationToken.None
            );
        }
        else if (result.MessageType == WebSocketMessageType.Close)
        {
            await webSocket.CloseAsync(
                closeStatus: WebSocketCloseStatus.NormalClosure,
                statusDescription: null,
                cancellationToken: CancellationToken.None
            );

            Console.WriteLine("WebSocket connection closed");
            break;
        }
    }
}