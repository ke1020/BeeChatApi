using System;
using Ke.Chat.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Ke.Chat.Chats;

public class MessageFragmentRepository(IDbContextProvider<ChatDbContext> dbContextProvider) 
    : EfCoreRepository<ChatDbContext, MessageFragment, Guid>(dbContextProvider), 
    IMessageFragmentRepository
{
    
}