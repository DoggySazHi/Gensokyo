using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gensokyo.Ran;

public class RanConfig
{
    [JsonPropertyName("gensokyo_url")] public string GensokyoUrl { get; init; } = "ws://gensokyo.williamle.com/ran";
    [JsonPropertyName("reconnect_timeout")] public int ReconnectTimeout { get; init; } = 60;
    [JsonPropertyName("client_secret")] public string ClientSecret { get; init; } = null!;
    
    [JsonPropertyName("jobs")] public Dictionary<string, RanJob> Jobs { get; init; } = new();
    
    public static RanConfig Load(string path = "config.json")
    {
        return JsonSerializer.Deserialize<RanConfig>(File.ReadAllText(path)) ?? throw new InvalidOperationException("Failed to load config.");
    }
}

public class RanJob
{
    [JsonPropertyName("executable")] public string Executable { get; init; } = null!;
    [JsonPropertyName("arguments")] public string? Arguments { get; init; }
    [JsonPropertyName("timeout")] public int Timeout { get; init; } = 60;
    [JsonPropertyName("async")] public bool Async { get; init; } = false;
    [JsonPropertyName("allowed_clients")] public string[]? AllowedClients { get; init; }
}
