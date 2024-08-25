using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gensokyo.Ran;

public class RanConfig
{
    [JsonPropertyName("gensokyo_url")] public string GensokyoUrl { get; set; } = "ws://gensokyo.williamle.com/ran";
    [JsonPropertyName("reconnect_timeout")] public int ReconnectTimeout { get; set; } = 60;
    [JsonPropertyName("public_key_file")] public string PublicKeyFile { get; set; } = "public.pem";
    [JsonPropertyName("private_key_file")] public string PrivateKeyFile { get; set; } = "private.pem";
    
    public static RanConfig Load(string path = "config.json")
    {
        return JsonSerializer.Deserialize<RanConfig>(File.ReadAllText(path)) ?? throw new InvalidOperationException("Failed to load config.");
    }
}