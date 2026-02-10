using Ke.Tasks.Models.Chats;

namespace Ke.Tasks.Abstractions;

public interface IChatSessionAppService
{
    Task CreateAsync(CreateChatSessionDto chatSession, 
        CancellationToken cancellationToken = default)
        ;
    Task AddMessageAsync(Guid sessionId, 
        ChatMessageInputDto input, 
        CancellationToken cancellationToken = default)
        ;
}