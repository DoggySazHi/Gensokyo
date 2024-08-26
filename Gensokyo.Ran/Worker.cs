using System.Net.WebSockets;
using System.Text.Json;
using Gensokyo.Yakumo.Models;
using Websocket.Client;

namespace Gensokyo.Ran;

public class Worker(ILogger<Worker> logger, RanConfig config, IHostApplicationLifetime host) : BackgroundService
{
    private WebsocketClient? _client;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client = new WebsocketClient(new Uri(config.GensokyoUrl));
        
        _client.ReconnectTimeout = TimeSpan.FromSeconds(config.ReconnectTimeout);
        _client.ReconnectionHappened.Subscribe(info =>
        {
            logger.LogInformation("Reconnection to Gensokyo occurred: {ReconnectionType}", info.Type);
        });

        _client.MessageReceived.Subscribe(OnReceive);

        await _client.Start();
        await stoppingToken;

        if (_client.IsStarted)
        {
            await _client.Stop(WebSocketCloseStatus.NormalClosure, "Worker stopped.");
        }

        _client.Dispose();
    }
    
    private void OnReceive(ResponseMessage message)
    {
        try
        {
            if (message.Text == null)
            {
                throw new InvalidOperationException("Message sent from server was null");
            }

            var job = JsonSerializer.Deserialize<JobRequest>(message.Text);
            
            if (job == null)
            {
                throw new InvalidOperationException("Failed to deserialize job request");
            }
            
            switch (job.JobName)
            {
                case "connection_response":
                    OnReceiveConnectionResponse(job.JobData);
                    break;
                case "heartbeat":
                    OnReceiveHeartbeat(job);
                    break;
                case "job_request":
                    OnReceiveJobRequest(job);
                    break;
                default:
                    _client?.Stop(WebSocketCloseStatus.InvalidMessageType, "Unsupported message type.");
                    throw new InvalidOperationException("Unsupported message type");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process message, killing worker");
            host.StopApplication();
        }
    }

    private void OnReceiveConnectionResponse(string data)
    {
        var response = JsonSerializer.Deserialize<ConnectionResponse>(data);
        
        if (response == null)
        {
            throw new InvalidOperationException("Failed to deserialize connection response");
        }
        
        logger.LogInformation("Connection status: {Success} {Reason}", response.Success, Enum.GetName(typeof(ConnectionReason), response.Reason));

        if (response.Success) return;
        
        _client?.Stop(WebSocketCloseStatus.NormalClosure, "Connection failed.");
        throw new InvalidOperationException("Connection failed");
    }

    private void OnReceiveHeartbeat(JobRequest job)
    {
        var response = JsonSerializer.Deserialize<Heartbeat>(job.JobData);
        
        if (response == null)
        {
            throw new InvalidOperationException("Failed to deserialize heartbeat response");
        }
     
        logger.LogDebug("Received heartbeat sent at {Time}", response.Timestamp);
        response.Acknowledged = DateTimeOffset.UtcNow;
        
        _client?.Send(JsonSerializer.Serialize(new JobResponse
        {
            JobId = job.JobId,
            Success = true,
            Async = false,
            Result = JsonSerializer.Serialize(response)
        }));
    }
    
    private void OnReceiveJobRequest(JobRequest job)
    {
        logger.LogInformation("Received job request: {JobName}", job.JobName);
    }
}