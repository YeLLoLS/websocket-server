using src.Services;
using System.Net;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<WebSocketService>();
var app = builder.Build();

app.UseWebSockets();

var connections = new List<WebSocket>();

app.Map("/ws", async (HttpContext context, WebSocketService webSocketService) =>
{
    await webSocketService.HandleWebSocketConnection(context);
});

await app.RunAsync();
