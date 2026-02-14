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
    public List<string>? RefFileIds { get; set; }
    /// <summary>
    /// 是否启用思考
    /// </summary>
    public bool ThinkingEnabled { get; set; } = false;
}