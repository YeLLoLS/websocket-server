using System.Net.WebSockets;

namespace src.Services
{
    internal class Player
    {
        public required string Name { get; set; }
        public int? Roll { get; set; }
        public required WebSocket Socket { get; set; }
    }
}