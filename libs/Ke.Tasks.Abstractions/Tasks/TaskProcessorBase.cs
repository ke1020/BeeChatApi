using System.Threading.Channels;
using Ke.Tasks.Models;
using Ke.Tasks.SSE.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ke.Tasks.Abstractions;

public abstract class TaskProcessorBase<T>(IServiceProvider serviceProvider) 
    : ITaskProcessor 
    where T : ITaskProcessor
{
    public event EventHandler<TaskCompletedEventArgs>? TaskCompleted;
    public event EventHandler<TaskItemCompletedEventArgs>? TaskItemCompleted;

    protected IServiceProvider ServiceProvider { get; } = serviceProvider;
    protected ILogger Logger { get; } = serviceProvider.GetRequiredService<ILogger<T>>();

    public abstract Task ProcessAsync(TaskInfo task,
        ChannelWriter<SseEvent> channelWriter,
        CancellationToken cancellationToken)
        ;

    protected virtual void OnTaskItemCompleted(TaskItem item)
    {
        TaskItemCompleted?.Invoke(this, new TaskItemCompletedEventArgs(item));
    }

    protected virtual void OnTaskCompleted(TaskInfo taskInfo)
    {
        TaskCompleted?.Invoke(this, new TaskCompletedEventArgs(taskInfo));
    }
}