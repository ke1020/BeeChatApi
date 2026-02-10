
namespace Ke.Tasks.Models;

/// <summary>
/// 单个文件处理项
/// </summary>
public class TaskItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string? FileName { get; set; } = string.Empty;
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = [];
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
}