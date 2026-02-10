using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using System;
using Ke.Chat.EntityFrameworkCore;

namespace Ke.Chat.Chats;

public class ChatSessionRepository : EfCoreRepository<ChatDbContext, ChatSession, Guid>, IChatSessionRepository
{
    public ChatSessionRepository(IDbContextProvider<ChatDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }
}