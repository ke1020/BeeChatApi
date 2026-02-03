using Ke.Tasks.Models.Chats;

namespace Ke.Tasks.Abstractions;

public interface IChat
{
    IAsyncEnumerable<object> SendAsync(ChatRequest request,
        CancellationToken cancellationToken = default);
}