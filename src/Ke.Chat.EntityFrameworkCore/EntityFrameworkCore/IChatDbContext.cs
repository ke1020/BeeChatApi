using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Ke.Chat.Chats;
using Microsoft.EntityFrameworkCore;

namespace Ke.Chat.EntityFrameworkCore;

[ConnectionStringName(ChatDbProperties.ConnectionStringName)]
public interface IChatDbContext : IEfCoreDbContext
{
    DbSet<ChatSession> ChatSessions { get; }
    DbSet<ChatMessage> ChatMessages { get; }
    DbSet<MessageFragment> MessageFragments { get; }
}
