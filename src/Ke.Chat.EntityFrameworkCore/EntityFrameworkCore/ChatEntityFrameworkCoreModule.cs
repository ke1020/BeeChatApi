using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;
using Ke.Chat.Chats;

namespace Ke.Chat.EntityFrameworkCore;

[DependsOn(
    typeof(ChatDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class ChatEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<ChatDbContext>(options =>
        {
            options.AddRepository<ChatSession, ChatSessionRepository>();
            options.AddRepository<ChatMessage, ChatMessageRepository>();
            options.AddRepository<MessageFragment, MessageFragmentRepository>();
        });
    }
}
