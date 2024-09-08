using Gensokyo.Yukari.Models;
using Gensokyo.Yukari.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gensokyo.Yukari.Routes;

[ApiController]
public class ApiController(YukariConfig config, WorkerManager manager) : ControllerBase
{
    [HttpPost("/api")]
    public async Task<ApiJobResponse> Post([FromBody] ApiJobRequest job)
    {
        // Check Bearer token
        if (!HttpContext.Request.Headers.TryGetValue("Authorization", out var authHeader) || !authHeader.ToString().StartsWith("Bearer "))
        {
            HttpContext.Response.StatusCode = 401;
            return new ApiJobResponse(false, "Unauthorized");
        }

        var authFound = config.AccessTokens.TryGetValue(authHeader.ToString()[7..], out var auth);
        
        if (!authFound || auth == null)
        {
            HttpContext.Response.StatusCode = 403;
            return new ApiJobResponse(false, "Forbidden");
        }
        
        return await manager.SendJob(job.JobName, job.JobData, auth.Name);
    }
}