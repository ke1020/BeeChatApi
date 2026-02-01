using Ke.Tasks.Models;
using Ke.Tasks.SSE.Models;

namespace Ke.Tasks.Abstractions;

/// <summary>
/// 任务通知接口
/// </summary>
public interface ITaskNotification<T> where T : TaskNotificationRequest
{
    IAsyncEnumerable<SseEvent> Send(T request,
        CancellationToken cancellationToken = default)
        ;
}