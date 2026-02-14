namespace Ke.Tasks.Models.Chats;

public class ChatMessageInputDto
{
    public int MessageId { get; set; }
    public int? ParentId { get; set; }
    public string? Model { get; set; }
    public string? Role { get; set; } = "USER";
    public bool ThinkingEnabled { get; set; } = false;
    public bool BanEdit { get; set; } = false;
    public bool BanRegenerate { get; set; } = false;
    public string Status { get; set; } = "PENDING";
    public int AccumulatedTokenUsage { get; set; } = 0;
    public List<string>? Files { get; set; }
    public bool SearchEnabled { get; set; } = false;
    /// <summary>
    /// 片段
    /// </summary>
    public List<ChatMessageFragment>? Fragments { get; set; }
}

public class ChatMessageFragment
{
    public string? Type { get; set; }
    public string? Content { get; set; }
    public TaskInfo? Task { get; set; }
}