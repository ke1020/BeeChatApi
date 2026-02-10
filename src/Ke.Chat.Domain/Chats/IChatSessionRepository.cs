using System;
using Volo.Abp.Domain.Repositories;

namespace Ke.Chat.Chats;

public interface IChatSessionRepository : IRepository<ChatSession, Guid>
{
    
}