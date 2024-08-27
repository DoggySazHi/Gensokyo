using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Gensokyo.Yakumo.Models;

namespace Gensokyo.Yukari.Services;

public class WorkerManager : IDisposable, IAsyncDisposable
{
    private class WebSocketSession
    {
        public byte[] Buffer { get; set; } = new byte[8 * 1024];
        public WebSocket WebSocket { get; set; }
        public DateTimeOffset LastHeartbeat { get; set; }

        public async Task SendAsync(object toSend)
        {
            var json = JsonSerializer.Serialize(toSend);
            var bytes = Encoding.UTF8.GetBytes(json);
            var buffer = new ArraySegment<byte>(bytes);
            await WebSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
    
    private readonly Timer _timer;
    private readonly IHostApplicationLifetime _lifetime;
    private List<WebSocketSession> _sessions = new();
    private Dictionary<string, WebSocketSession> _clientToSession = new();
    private ulong _jobId = 0;

    public WorkerManager(IHostApplicationLifetime lifetime)
    {
        _timer = new Timer(PeriodicLoop, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        _lifetime = lifetime;
    }
    
    private void PeriodicLoop(object? state)
    {
        var now = DateTimeOffset.UtcNow;
        
        var toRemove = new List<WebSocketSession>();
        
        foreach (var session in _sessions)
        {
            if (now - session.LastHeartbeat > TimeSpan.FromSeconds(120))
            {
                session.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "No heartbeat received", _lifetime.ApplicationStopping);
                toRemove.Add(session);
            }
            else if (now - session.LastHeartbeat > TimeSpan.FromSeconds(30))
            {
                _ = session.SendAsync(new JobRequest
                {
                    JobId = Interlocked.Increment(ref _jobId).ToString(),
                    JobName = "heartbeat",
                    JobData = JsonSerializer.Serialize(new Heartbeat { Timestamp = now }),
                    ClientName = "yukari"
                });
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