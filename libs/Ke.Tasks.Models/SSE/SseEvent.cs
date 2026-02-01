using System;
using System.Text.Json.Serialization;

namespace Ke.Tasks.SSE.Models;

public class SseEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("event")]
    public string? EventType { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }

    [JsonPropertyName("retry")]
    public int? Retry { get; set; }

    [JsonIgnore]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}