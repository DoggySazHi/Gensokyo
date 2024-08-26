using System.Text.Json.Serialization;

namespace Gensokyo.Yakumo.Models;

public class JobRequest
{
    [JsonPropertyName("job_id")] public string JobId { get; init; } = null!;
    [JsonPropertyName("job_name")] public string JobName { get; init; } = null!;
    [JsonPropertyName("job_data")] public string JobData { get; init; } = null!;
    [JsonPropertyName("client_name")] public string ClientName { get; init; } = null!;
}

public class JobResponse
{
    [JsonPropertyName("job_id")] public string JobId { get; init; } = "";
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("async")] public bool Async { get; init; }
    [JsonPropertyName("result")] public string? Result { get; init; }
}