
namespace Ke.Tasks.Models;

/// <summary>
/// 任务信息
/// </summary>
public class TaskInfo
{
    public string TaskId { get; set; } = Guid.NewGuid().ToString();
    public string TaskName { get; set; } = string.Empty;
    /// <summary>
    /// 输入文件列表
    /// </summary>
    public string[] InputFiles { get; set; } = [];
    /// <summary>
    /// 输出文件列表
    /// </summary>
    public string[]? OutputFiles { get; set; }
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
}