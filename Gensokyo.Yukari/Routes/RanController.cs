using Microsoft.AspNetCore.Mvc;

namespace Gensokyo.Yukari.Routes;

public class RanController : ControllerBase
{
    [Route("/ran")]
    public async Task Get()
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            
        }
    }
}