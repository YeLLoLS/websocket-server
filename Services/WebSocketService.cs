using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace src.Services
{
    public class WebSocketService
    {
        private readonly List<Player> _players;

        public WebSocketService()
        {
            _players = new List<Player>();
        }

        public async Task HandleWebSocketConnection(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                using var ws = await context.WebSockets.AcceptWebSocketAsync();
                if(_players.Count == 2)
                {
                    await BroadCastSpecificConnection($"Sorry, the room is full!", ws);
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Sorry, the room is full!", CancellationToken.None);
                    return;
                }

                if (!context.Request.Headers.TryGetValue("Name", out Microsoft.Extensions.Primitives.StringValues value))
                {
                    //context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    await ws.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Name not found in request header", CancellationToken.None);
                    return;
                }
                var curName = value;

                _players.Add(new Player
                {
                    Name = curName,
                    Socket = ws
                });

                await Broadcast($"{curName} joined the room");
                await Broadcast($"{_players.Count} users connected");
                await ReceiveMessage(ws, async (result, buffer) =>
                {
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await HandleRollMessage(curName, message);
                        if(_players.Count == 2 && _players.All(x => x.Roll != null))
                        {
                            await DecideWinner();
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Close || ws.State == WebSocketState.Aborted)
                    {
                        _players.RemoveAll(p => p.Socket == ws);
                        await Broadcast($"{curName} left the room");
                        await Broadcast($"{_players.Count} users connected");
                        await ws.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                    }
                });
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
        }

        private async Task DecideWinner()
        {
            var roll = _players.Max(x => x.Roll);
            if (_players.All(x => x.Roll == roll))
            {
                await Broadcast($"It's a tie with a roll of {roll}");
            }
            var player = _players.FirstOrDefault(x => x.Roll == roll);
            await BroadCastSpecificConnection($"GJ {player.Name}, you WON with a roll of {player.Roll}", player.Socket);
            await Broadcast($"{player.Name} won with a roll of {player.Roll}");
            _players.ForEach(x => x.Roll = null);
        }

        private async Task ReceiveMessage(WebSocket socket, Action<WebSocketReceiveResult, byte[]> handleMessage)
        {
            var buffer = new byte[1024 * 4];
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                handleMessage(result, buffer);
            }
        }

        private async Task HandleRollMessage(string playerName, string message)
        {
            if (message.Equals("roll", StringComparison.OrdinalIgnoreCase))
            {
                var player = _players.FirstOrDefault(p => p.Name == playerName);
                if(!string.IsNullOrEmpty(player.Roll.ToString()))
                {
                    await BroadCastSpecificConnection($"You have already rolled! Wait for other player to roll!", player.Socket);
                    return;
                }
                await AddRollToPlayer(playerName);
            }
            else
            {
                await Broadcast($"{playerName}: {message}");
            }
        }

        private async Task AddRollToPlayer(string playerName)
        {
            var randomNumber = new Random().Next(1, 7);
            _players.FirstOrDefault(p => p.Name == playerName).Roll = randomNumber;
            await Broadcast($"{playerName} rolled {randomNumber}");
            await Task.CompletedTask;
        }

        private async Task Broadcast(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            foreach (var socket in _players)
            {
                if (socket.Socket.State == WebSocketState.Open)
                {
                    var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length);
                    await socket.Socket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }

        private async Task BroadCastSpecificConnection(string message, WebSocket socket)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            if (socket.State == WebSocketState.Open)
            {
                var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length);
                await socket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }
}
