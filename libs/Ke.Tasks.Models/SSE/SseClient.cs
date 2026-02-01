namespace Ke.Tasks.SSE.Models;

public class SseClient(string clientId)
{
    public string ClientId { get; } = clientId;
    public string LastEventId { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; }
}