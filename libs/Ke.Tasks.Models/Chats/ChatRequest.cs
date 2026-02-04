namespace Ke.Tasks.Models.Chats;

/// <summary>
/// 对话请求
/// </summary>
public class ChatRequest
{
    /// <summary>
    /// 会话 ID
    /// </summary>
    public Guid SessionId { get; set; }
    /// <summary>
    /// 父级 ID
    /// </summary>
    public int? ParentId { get; set; }
    /// <summary>
    /// 提示词
    /// </summary>
    public required string Prompt { get; set; }
    /// <summary>
    /// 任务类型
    /// </summary>
    public string? TaskType { get; set; }
    /// <summary>
    /// 参考文件 ID
    /// </summary>
    public string[]? RefFileIds { get; set; }
    /// <summary>
    /// 客户端流 ID
    /// </summary>
    public string? ClientStreamId { get; set; }
    /// <summary>
    /// 是否启用思考
    /// </summary>
    public bool ThinkingEnabled { get; set; } = false;
}

/// <summary>
/// 会话
/// </summary>
public class ChatSession
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string? Title { get; set; }
    public bool Pinned { get; set; } = false;
    public DateTime CreateAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdateAt { get; set; } = DateTime.UtcNow;
    public int CurrentMessageId { get; set; }
}