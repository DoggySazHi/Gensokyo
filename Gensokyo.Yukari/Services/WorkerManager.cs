using System.Net.WebSockets;

namespace Gensokyo.Yukari.Services;

public class WorkerManager : IDisposable, IAsyncDisposable
{
    private class WebSocketSession
    {
        public byte[] Buffer { get; set; } = new byte[8 * 1024];
        public WebSocket WebSocket { get; set; }
        public DateTimeOffset LastHeartbeat { get; set; }
    }
    
    private readonly Timer _timer;
    private readonly IHostApplicationLifetime _lifetime;
    private List<WebSocketSession> _sessions = new();
    private Dictionary<string, WebSocketSession> _clientToSession = new();

    public WorkerManager(IHostApplicationLifetime lifetime)
    {
        _timer = new Timer(PeriodicLoop, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        _lifetime = lifetime;
    }
    
    private void PeriodicLoop(object? state)
    {
        var now = DateTimeOffset.UtcNow;
        
        foreach (var (id, session) in _clientToSession)
        {
            if (now - session.LastHeartbeat > TimeSpan.FromSeconds(120))
            {
                _clientToSession.Remove(id);
            }
            else if (now - session.LastHeartbeat > TimeSpan.FromSeconds(30))
            {
                // Send a heartbeat message
            }
        }
    }
    
    public void AddWebSocket(WebSocket webSocket)
    {
        var session = new WebSocketSession
        {
            WebSocket = webSocket,
            LastHeartbeat = DateTimeOffset.UtcNow
        };
        
        _sessions.Add(session);
        
        Task.Run(async () => await SocketEventHandler(session));
    }

    private async Task SocketEventHandler(WebSocketSession session)
    {
        var receiveBuffer = new ArraySegment<byte>(session.Buffer);
        var receiveResult = await session.WebSocket.ReceiveAsync(receiveBuffer, _lifetime.ApplicationStopping);
        
        while (!receiveResult.CloseStatus.HasValue)
        {
            if (receiveResult.MessageType == WebSocketMessageType.Text)
            {
                // Handle message
            }
            
            receiveResult = await session.WebSocket.ReceiveAsync(receiveBuffer, _lifetime.ApplicationStopping);
        }
        
        await session.WebSocket.CloseAsync(receiveResult.CloseStatus.Value, receiveResult.CloseStatusDescription, _lifetime.ApplicationStopping);
        
        _sessions.Remove(session);
    }

    public void Dispose()
    {
        _timer.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await _timer.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}