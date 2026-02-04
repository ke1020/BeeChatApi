using System.Threading.Channels;
using Ke.Tasks.Models;
using Ke.Tasks.SSE.Models;

namespace Ke.Tasks.Abstractions;

public interface ITaskProcessor
{
    /// <summary>
    /// 处理任务
    /// </summary>
    /// <param name="task"></param>
    /// <param name="channelWriter"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task ProcessAsync(TaskInfo task, ChannelWriter<SseEvent> channelWriter, CancellationToken cancellationToken);
}