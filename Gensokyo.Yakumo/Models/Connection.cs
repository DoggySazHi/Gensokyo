using System.Text.Json.Serialization;

namespace Gensokyo.Yakumo.Models;

public enum ConnectionReason
{
    Success,
    InvalidKey,
    InvalidName,
    InvalidJobs
}

public class ConnectionRequest
{
    [JsonPropertyName("client_secret")] public string? ClientSecret { get; set; }
    [JsonPropertyName("friendly_name")] public string? FriendlyName { get; set; }
    [JsonPropertyName("jobs_available")] public string[]? JobsAvailable { get; set; }
}

public class ConnectionResponse
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("reason")] public ConnectionReason Reason { get; set; }
}