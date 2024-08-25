using Websocket.Client;

namespace Gensokyo.Ran;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly RanConfig _config;
    private WebsocketClient? _client;

    public Worker(ILogger<Worker> logger, RanConfig config)
    {
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client = new WebsocketClient(new Uri(_config.GensokyoUrl));
        
        _client.ReconnectTimeout = TimeSpan.FromSeconds(_config.ReconnectTimeout);
        _client.ReconnectionHappened.Subscribe(info =>
        {
            _logger.LogInformation("Reconnection to Gensokyo occurred: {ReconnectionType}", info.Type);
        });

        _client.MessageReceived.Subscribe(OnReceive);

        await _client.Start();
        await stoppingToken;
    }
    
    private void OnReceive(ResponseMessage message)
    {
        _logger.LogInformation("Ping! {Message}", message.Text);
        _client?.Send("pong");
    }
}