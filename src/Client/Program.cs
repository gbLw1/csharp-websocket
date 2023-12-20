using System.Net.WebSockets;
using System.Text;
using static System.Console;

Uri uri = new("ws://localhost:5100/");

using var ws = new ClientWebSocket();

await ws.ConnectAsync(uri, CancellationToken.None);

var buffer = new byte[1024];

while (ws.State == WebSocketState.Open)
{
    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

    if (result.MessageType == WebSocketMessageType.Close)
    {
        await ws.CloseAsync(
            closeStatus: WebSocketCloseStatus.NormalClosure,
            statusDescription: null,
            cancellationToken: CancellationToken.None
        );
    }
    else
    {
        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
        WriteLine($"Received message: {message}");
    }
}
