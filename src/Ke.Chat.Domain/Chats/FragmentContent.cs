using System;
using System.Collections.Generic;
using Ke.Chat.Chats;
using Volo.Abp.Domain.Values;

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
public class TaskFragmentContent(TaskInfo taskInfo) : FragmentContent
{
    public TaskInfo Task { get; private set; } = taskInfo 
        ?? throw new ArgumentNullException(nameof(taskInfo))
        ;

    public override FragmentContentType Type => FragmentContentType.Task;

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return Task;
    }
}