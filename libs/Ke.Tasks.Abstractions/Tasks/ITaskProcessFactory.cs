using Ke.Tasks.Models;

namespace Ke.Tasks.Abstractions;

/// <summary>
/// 任务处理器工厂
/// </summary>
public interface ITaskProcessFactory
{
    /// <summary>
    /// 创建任务处理器
    /// </summary>
    /// <param name="taskType"></param>
    /// <returns></returns>
    ITaskProcessor Create(TaskType taskType);
}