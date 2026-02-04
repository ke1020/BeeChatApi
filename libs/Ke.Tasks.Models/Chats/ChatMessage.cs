namespace Ke.Tasks.Models.Chats;

public class ChatMessage
{
    public Guid Id { get; set; }
    public int MessageId { get; set; }
    public int ParentId { get; set; }
    public string? Model { get; set; }
    public string? Role { get; set; } = "USER";
    public bool ThinkingEnabled { get; set; } = false;
    public bool BanEdit { get; set; } = false;
    public bool BanRegenerate { get; set; } = false;
    public string Status { get; set; } = "PENDING";
    public int AccumulatedTokenUsage { get; set; } = 0;
    public string[]? Files { get; set; } = [];
    //"feedback": null,
    //"inserted_at": 1769998615.431,
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool SearchEnabled { get; set; } = false;
    /*
    "fragments": [
        {
            "id": 1,
            "type": "REQUEST",
            "content": "FFmpegCore 如何取消任务"
        }
    ],
    "has_pending_fragment": false,
    "auto_continue": false
    */

    public object[]? Fragments { get; set; }
}