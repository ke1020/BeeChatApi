using Ke.Tasks.Models.Chats;
using Ke.Tasks.SSE.Models;

namespace Ke.Tasks.Abstractions;

public interface IChat
{
    IAsyncEnumerable<SseEvent> SendAsync(ChatRequest request,
        CancellationToken cancellationToken = default);
}