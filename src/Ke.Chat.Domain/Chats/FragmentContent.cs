using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Values;

namespace Ke.Chat.Chats;

/// <summary>
/// 片段内容基类
/// </summary>
public abstract class FragmentContent : ValueObject
{
    /// <summary>
    /// 片段类型
    /// </summary>
    public abstract FragmentContentType Type { get; }
}

/// <summary>
/// 文本片段内容
/// </summary>
public class TextFragmentContent : FragmentContent
{
    public string Text { get; private set; }

    public override FragmentContentType Type => FragmentContentType.Text;

    public TextFragmentContent(string text)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return Text;
    }
}

/// <summary>
/// 任务片段内容
/// </summary>
public class TaskFragmentContent(TaskInfo task) : FragmentContent
{
    public TaskInfo Task { get; private set; } = task
        ?? throw new ArgumentNullException(nameof(task))
        ;

    public override FragmentContentType Type => FragmentContentType.Task;

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return Task;
    }
}

/// <summary> 任务信息（用于跟踪和管理单个任务）</summary>
public sealed class TaskInfo
{
    /// <summary>任务唯一标识</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();
    /// <summary>任务类型（ASR、TTS、音视频分离等）</summary>
    public TaskType TaskType { get; set; }
    /// <summary>当前状态</summary>
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    /// <summary>开始时间（UTC）</summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    /// <summary>结束时间（UTC）</summary>
    public DateTime? EndTime { get; set; }
    /// <summary>输入文件ID列表（来自请求）</summary>
    public List<string> InputFiles { get; set; } = [];
    /// <summary>输出文件ID列表（处理后生成）</summary>
    public List<string> OutputFiles { get; set; } = [];
    /// <summary>任务参数（如模型、语言、分辨率等）</summary>
    public Dictionary<string, object> Parameters { get; set; } = [];
    /// <summary>错误信息（失败时填充）</summary>
    public string? ErrorMessage { get; set; }
    /// <summary>子任务列表（每个文件或每个步骤的详细处理单元）</summary>
    public List<TaskItem> SubTasks { get; set; } = [];
}

/// <summary>子任务项（任务中的最小处理单元）</summary>
public class TaskItem
{
    /// <summary>子项唯一标识</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();
    /// <summary>输入文件标识（单个文件）</summary>
    public string? InputFile { get; set; }
    /// <summary>输出文件标识</summary>
    public string? OutputFile { get; set; }
    /// <summary>当前状态</summary>
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    /// <summary>开始时间</summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    /// <summary>结束时间</summary>
    public DateTime? EndTime { get; set; }
    /// <summary>错误信息</summary>
    public string? ErrorMessage { get; set; }
}