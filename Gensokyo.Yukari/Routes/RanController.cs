using Gensokyo.Yukari.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gensokyo.Yukari.Routes;

public class RanController(WorkerManager manager) : ControllerBase
{
    
    [Route("/ran")]
    public async Task Get()
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }
}