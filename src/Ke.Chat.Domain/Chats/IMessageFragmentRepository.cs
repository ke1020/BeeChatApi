using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Ke.Chat.Chats;

public interface IMessageFragmentRepository : IRepository<MessageFragment, Guid>
{
    
}