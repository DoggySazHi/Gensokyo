using Gensokyo.Yukari.Models;
using Gensokyo.Yukari.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gensokyo.Yukari.Routes;

[ApiController]
public class ApiController(YukariConfig config, WorkerManager manager) : ControllerBase
{
    [HttpPost("/api")]
    public async Task Post([FromBody] ApiJobRequest job)
    {
        // Check Bearer token
        if (!HttpContext.Request.Headers.TryGetValue("Authorization", out var authHeader) || !authHeader.ToString().StartsWith("Bearer "))
        {
            HttpContext.Response.StatusCode = 401;
            return;
        }

        var authFound = config.AccessTokens.TryGetValue(authHeader.ToString()[7..], out var auth);
        
        if (!authFound || auth == null)
        {
            HttpContext.Response.StatusCode = 403;
            return;
        }
        
        // Check if we have a job registered with the given name
    }
}