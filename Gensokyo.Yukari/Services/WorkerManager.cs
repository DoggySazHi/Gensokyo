using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Gensokyo.Yakumo.Models;
using Gensokyo.Yukari.Models;

namespace Gensokyo.Yukari.Services;

public class WorkerManager : IDisposable, IAsyncDisposable
{
    private class WebSocketSession
    {
        public byte[] Buffer { get; } = new byte[8 * 1024];
        public WebSocket WebSocket { get; }
        public TaskCompletionSource WebSocketTask { get; }
        public DateTimeOffset LastHeartbeat { get; set; }
        public string[] Jobs { get; set; } = [];
        
        public WebSocketSession(WebSocket webSocket, TaskCompletionSource socketTask)
        {
            WebSocket = webSocket;
            WebSocketTask = socketTask;
            LastHeartbeat = DateTimeOffset.UtcNow;
        }

        public async Task SendAsync(object toSend)
        {
            var json = JsonSerializer.Serialize(toSend);
            var bytes = Encoding.UTF8.GetBytes(json);
            var buffer = new ArraySegment<byte>(bytes);
            await WebSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }
        
        public async Task CloseAsync(WebSocketCloseStatus status, string? description, CancellationToken token)
        {
            await WebSocket.CloseAsync(status, description, token);
            WebSocketTask.SetResult();
        }
    }
    
    private readonly Timer _timer;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly List<WebSocketSession> _sessions = [];
    private readonly Dictionary<string, TaskCompletionSource<JobResponse>> _jobTasks = new();
    private readonly ILogger<WorkerManager> _logger;
    private readonly YukariConfig _config;
    private ulong _jobId;

    public WorkerManager(IHostApplicationLifetime lifetime, ILogger<WorkerManager> logger, YukariConfig config)
    {
        _timer = new Timer(PeriodicLoop, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        _lifetime = lifetime;
        _logger = logger;
        _config = config;
    }
    
    private JobRequest CreateJobRequest(string jobName, object jobData, string? jobId = null)
    {
        return new JobRequest
        {
            JobId = jobId + Interlocked.Increment(ref _jobId),
            JobName = jobName,
            JobData = JsonSerializer.Serialize(jobData),
            ClientName = "yukari"
        };
    }
    
    private void PeriodicLoop(object? state)
    {
        var now = DateTimeOffset.UtcNow;
        
        var toRemove = new List<WebSocketSession>();
        
        foreach (var session in _sessions)
        {
            if (session.WebSocket.State != WebSocketState.Open)
            {
                toRemove.Add(session);
            }
            else if (now - session.LastHeartbeat > TimeSpan.FromSeconds(120))
            {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                session.CloseAsync(WebSocketCloseStatus.NormalClosure, "No heartbeat received", _lifetime.ApplicationStopping);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                toRemove.Add(session);
            }
            else if (now - session.LastHeartbeat > TimeSpan.FromSeconds(30))
            {
                _ = session.SendAsync(CreateJobRequest("heartbeat", new Heartbeat { Timestamp = now }, "heartbeat"));
            }
        }
        
        foreach (var session in toRemove)
        {
            _sessions.Remove(session);
        }
    }
    
    public void AddWebSocket(WebSocket webSocket, TaskCompletionSource socketTask)
    {
        var session = new WebSocketSession(webSocket, socketTask);
        
        _sessions.Add(session);
        
        Task.Run(async () => await SocketEventHandler(session));
    }
    
    public async Task<ApiJobResponse> SendJob(string jobName, string jobData, string clientName)
    {
        var session = _sessions.FirstOrDefault(o => o.Jobs.Contains(jobName));
        
        if (session == null)
        {
            return new ApiJobResponse(false, "No worker available");
        }

        var jobId = Interlocked.Increment(ref _jobId).ToString();
        
        await session.SendAsync(CreateJobRequest("job_request", new JobRequest
        {
            JobId = jobId,
            JobName = jobName,
            JobData = jobData,
            ClientName = clientName
        }));
        
        var task = new TaskCompletionSource<JobResponse>();
        _jobTasks[jobId] = task;
        
        var response = await task.Task;
        
        _jobTasks.Remove(jobId);
        
        return new ApiJobResponse(response.Success, response.Result);
    }

    private async Task SocketEventHandler(WebSocketSession session)
    {
        var receiveBuffer = new ArraySegment<byte>(session.Buffer);
        var receiveResult = await session.WebSocket.ReceiveAsync(receiveBuffer, _lifetime.ApplicationStopping);
        
        ConnectionRequest? connectionRequest = null;

        try
        {
            connectionRequest = JsonSerializer.Deserialize<ConnectionRequest>(Encoding.UTF8.GetString(receiveBuffer.Array!.AsSpan(0, receiveResult.Count)));
        } catch (Exception) { /* ignored */ }
        
        if (connectionRequest == null)
        {
            await session.SendAsync(CreateJobRequest("connection_response", new ConnectionResponse(false, ConnectionReason.InvalidPayload)));
            await session.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Failed to deserialize connection request", _lifetime.ApplicationStopping);
            _sessions.Remove(session);
            return;
        }
        
        // Check if the client is allowed to connect
        if (_config.AllowedClients != null && !_config.AllowedClients.Contains(connectionRequest.ClientSecret))
        {
            await session.SendAsync(CreateJobRequest("connection_response", new ConnectionResponse(false, ConnectionReason.InvalidKey)));
            await session.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Invalid client secret", _lifetime.ApplicationStopping);
            _sessions.Remove(session);
            return;
        }
        
        // Check if the client has any jobs available
        if (connectionRequest.JobsAvailable == null || connectionRequest.JobsAvailable.Length == 0)
        {
            await session.SendAsync(CreateJobRequest("connection_response", new ConnectionResponse(false, ConnectionReason.InvalidJobs)));
            await session.CloseAsync(WebSocketCloseStatus.PolicyViolation, "No jobs available", _lifetime.ApplicationStopping);
            _sessions.Remove(session);
            return;
        }
        
        if (connectionRequest.JobsAvailable.Contains("connection_response") || connectionRequest.JobsAvailable.Contains("heartbeat"))
        {
            await session.SendAsync(CreateJobRequest("connection_response", new ConnectionResponse(false, ConnectionReason.InvalidJobs)));
            await session.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Invalid job name", _lifetime.ApplicationStopping);
            _sessions.Remove(session);
            return;
        }
        
        if (string.IsNullOrWhiteSpace(connectionRequest.FriendlyName) || connectionRequest.FriendlyName == "yukari")
        {
            await session.SendAsync(CreateJobRequest("connection_response", new ConnectionResponse(false, ConnectionReason.InvalidName)));
            await session.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Invalid friendly name", _lifetime.ApplicationStopping);
            _sessions.Remove(session);
            return;
        }
        
        session.Jobs = connectionRequest.JobsAvailable;
        
        await session.SendAsync(CreateJobRequest("connection_response", new ConnectionResponse(true, ConnectionReason.Success)));
        
        _logger.LogInformation("Client {ClientName} connected", connectionRequest.FriendlyName);
        
        while (!receiveResult.CloseStatus.HasValue)
        {
            if (receiveResult.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(receiveBuffer.Array!.AsSpan(0, receiveResult.Count));

                try
                {
                    var jobResponse = JsonSerializer.Deserialize<JobResponse>(message);
                    
                    if (jobResponse == null)
                    {
                        _logger.LogWarning("Failed to deserialize job response");
                        continue;
                    }
                    
                    if (jobResponse.JobId.StartsWith("heartbeat") && jobResponse.Result != null)
                    {
                        var heartbeat = JsonSerializer.Deserialize<Heartbeat>(jobResponse.Result);
                        
                        if (heartbeat == null)
                        {
                            _logger.LogWarning("Failed to deserialize heartbeat response");
                            continue;
                        }
                        
                        session.LastHeartbeat = heartbeat.Timestamp;
                    }
                    else
                    {
                        _logger.LogInformation("Received job response: {JobId} {Success} {Async}", jobResponse.JobId, jobResponse.Success, jobResponse.Async);
                        _logger.LogDebug("Result: {Result}", jobResponse.Result);

                        if (_jobTasks.TryGetValue(jobResponse.JobId, out var task))
                        {
                            task.SetResult(jobResponse);
                        }
                    }
                }
                catch (Exception)
                {
                    _logger.LogWarning("Failed to deserialize job response for {Name}", connectionRequest.FriendlyName);
                }
            }
            
            receiveResult = await session.WebSocket.ReceiveAsync(receiveBuffer, _lifetime.ApplicationStopping);
        }
        
        await session.CloseAsync(receiveResult.CloseStatus.Value, receiveResult.CloseStatusDescription, _lifetime.ApplicationStopping);
        
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