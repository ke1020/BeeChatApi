using Ke.Tasks.Abstractions;
using Ke.Tasks.Models;
using Ke.Tasks.Processors;

namespace Ke.Tasks;

/// <summary>
/// 任务处理器工厂
/// </summary>
public class TaskProcessFactory : ITaskProcessFactory
{
    /// <summary>
    /// 服务提供器
    /// </summary>
    private readonly IServiceProvider _serviceProvider;
    /// <summary>
    /// 存储管理服务
    /// </summary>
    private readonly Dictionary<TaskType, Type> _providerTypes;

    public TaskProcessFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _providerTypes = new Dictionary<TaskType, Type>()
            {
                { TaskType.ASR, typeof(AsrTaskProcessor) },
                { TaskType.TTS, typeof(TtsTaskProcessor) },
            };
    }

    /// <summary>
    /// 创建任务处理器
    /// </summary>
    /// <param name="taskType"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public ITaskProcessor Create(TaskType taskType)
    {
        return _serviceProvider.GetService(_providerTypes[taskType]) as ITaskProcessor ??
            throw new NotSupportedException($"Not supported task type: {taskType}")
            ;
    }
}