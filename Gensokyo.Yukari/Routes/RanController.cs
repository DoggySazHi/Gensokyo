using Gensokyo.Yukari.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gensokyo.Yukari.Routes;

[ApiController]
public class RanController(WorkerManager manager, ILogger<RanController> logger) : ControllerBase
{
    [Route("/ran")]
    public async Task Get()
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            manager.AddWebSocket(webSocket);
            logger.LogInformation("WebSocket connection established with {RemoteAddress}", HttpContext.Connection.RemoteIpAddress);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
            logger.LogWarning("Non-WebSocket request received from {RemoteAddress}", HttpContext.Connection.RemoteIpAddress);
        }
    }
}