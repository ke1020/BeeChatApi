
using System.Threading.Channels;
using Ke.Tasks.Abstractions;
using Ke.Tasks.Models;
using Ke.Tasks.SSE.Models;

namespace Ke.Tasks.Processors;

public class TtsTaskProcessor : ITaskProcessor
{
    public Task ProcessAsync(TaskInfo task, ChannelWriter<SseEvent> channelWriter, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}