using System;

namespace Ke.Tasks.SSE.Models;

public class SseMessage
{
    public string Type { get; set; } = "message";
    public object Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static SseMessage Create(object content, string type = "message")
    {
        return new SseMessage
        {
            Type = type,
            Content = content,
            Timestamp = DateTime.UtcNow
        };
    }
}