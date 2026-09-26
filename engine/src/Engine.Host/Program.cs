using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using OpenLogViewer.Engine.Host;
using OpenLogViewer.Engine.Parsing;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");

var app = builder.Build();
var dispatcher = new CommandDispatcher(ParserRegistry.CreateDefault(), new LogStore(), LayoutStore.CreateDefault(), AnnotationStore.CreateDefault(), ChannelColorStore.CreateDefault(), RecentLogsStore.CreateDefault(), CustomMathChannelStore.CreateDefault());

app.UseWebSockets();

app.Map("/ws", async (HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var ws = await context.WebSockets.AcceptWebSocketAsync();
    await SendText(ws, dispatcher.HelloEvent());

    var buffer = new byte[64 * 1024];
    var message = new MemoryStream();
    while (ws.State == WebSocketState.Open)
    {
        WebSocketReceiveResult result;
        try
        {
            result = await ws.ReceiveAsync(buffer, CancellationToken.None);
        }
        catch (WebSocketException)
        {
            break;
        }

        if (result.MessageType == WebSocketMessageType.Close)
        {
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
            break;
        }

        message.Write(buffer, 0, result.Count);
        if (!result.EndOfMessage) continue;

        string request = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
        message.SetLength(0);
        await SendText(ws, dispatcher.Handle(request));
    }
});

app.Lifetime.ApplicationStarted.Register(() =>
{
    var address = app.Services.GetRequiredService<IServer>()
        .Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault();
    if (address is not null)
        Console.WriteLine($"ENGINE_READY {address.Replace("http://", "ws://")}/ws");
});

app.Run();

static Task SendText(WebSocket ws, string json)
    => ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationToken.None);
