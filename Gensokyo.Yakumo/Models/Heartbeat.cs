using System.Text.Json.Serialization;

namespace Gensokyo.Yakumo.Models;

/// <summary>
/// A heartbeat message to ensure the WebSocket connection is still alive.
/// </summary>
public class Heartbeat
{
    /// <summary>
    /// Written by Yukari to Ran.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }
    
    /// <summary>
    /// Written back by Ran to acknowledge.
    /// </summary>
    [JsonPropertyName("acknowledged")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? Acknowledged { get; set; }
}