using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Ke.Chat.Chats;

public interface IChatMessageRepository : IRepository<ChatMessage, Guid>
{
    Task<ChatMessage?> GetByMessageIdAsync(int messageId, CancellationToken cancellationToken = default);
    Task<List<ChatMessage>> GetByParentIdAsync(int? parentId, CancellationToken cancellationToken = default);
    Task<List<ChatMessage>> GetByStatusAsync(ChatStatus status, CancellationToken cancellationToken = default);
}
