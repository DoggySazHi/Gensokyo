using System.Text.Json.Serialization;

namespace Gensokyo.Yakumo.Models;

public enum ConnectionReason
{
    Success,
    InvalidKey,
    InvalidName,
    InvalidJobs,
    InvalidPayload
}

public class ConnectionRequest
{
    [JsonPropertyName("client_secret")] public string? ClientSecret { get; init; }
    [JsonPropertyName("friendly_name")] public string? FriendlyName { get; init; }
    [JsonPropertyName("jobs_available")] public string[]? JobsAvailable { get; init; }
}

public class ConnectionResponse(bool success, ConnectionReason reason)
{
    [JsonPropertyName("success")] public bool Success { get; init; } = success;
    [JsonPropertyName("reason")] public ConnectionReason Reason { get; init; } = reason;
}