using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using Ke.Chat.EntityFrameworkCore;

namespace Ke.Chat.Chats;

public class ChatMessageRepository : EfCoreRepository<ChatDbContext, ChatMessage, Guid>, IChatMessageRepository
{
    public ChatMessageRepository(IDbContextProvider<ChatDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<ChatMessage?> GetByMessageIdAsync(int messageId, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet.FirstOrDefaultAsync(cm => cm.MessageId == messageId, cancellationToken);
    }

    public async Task<List<ChatMessage>> GetByParentIdAsync(int? parentId, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet.Where(cm => cm.ParentId == parentId).ToListAsync(cancellationToken);
    }

    public async Task<List<ChatMessage>> GetByStatusAsync(ChatStatus status, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet.Where(cm => cm.Status == status).ToListAsync(cancellationToken);
    }

    public async Task<List<MessageFragment>> GetFragmentsByMessageIdAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        return await dbContext.Set<MessageFragment>()
            .Where(f => f.ChatMessageId == messageId)
            .ToListAsync(cancellationToken)
            ;
    }

    public async Task AddFragmentAsync(Guid messageId, MessageFragment fragment, CancellationToken cancellationToken = default)
    {
        fragment.ChatMessageId = messageId;

        var dbContext = await GetDbContextAsync();
        await dbContext.Set<MessageFragment>().AddAsync(fragment, cancellationToken);
    }

}
