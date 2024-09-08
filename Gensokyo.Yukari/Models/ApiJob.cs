using System.Text.Json.Serialization;

namespace Gensokyo.Yukari.Models;

public class ApiJobRequest
{
    [JsonPropertyName("job_name")] public string JobName { get; init; } = null!;
    [JsonPropertyName("job_data")] public string JobData { get; init; } = null!;
}

public class ApiJobResponse
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("result")] public string? Result { get; init; }
    
    public ApiJobResponse(bool success, string? result)
    {
        Success = success;
        Result = result;
    }
}