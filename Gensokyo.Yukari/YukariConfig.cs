using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gensokyo.Yukari;

public class YukariConfig
{
    [JsonPropertyName("access_tokens")] public Dictionary<string, YukariAccessToken> AccessTokens { get; init; } = [];
    [JsonPropertyName("allowed_clients")] public string[]? AllowedClients { get; init; }
    
    public static YukariConfig Load(string path = "config.json")
    {
        return JsonSerializer.Deserialize<YukariConfig>(File.ReadAllText(path)) ?? throw new InvalidOperationException("Failed to load config.");
    }
}

public class YukariAccessToken
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}
