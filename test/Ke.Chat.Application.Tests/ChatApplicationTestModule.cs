using Volo.Abp.Modularity;

namespace Ke.Chat;

[DependsOn(
    typeof(ChatApplicationModule),
    typeof(ChatDomainTestModule)
    )]
public class ChatApplicationTestModule : AbpModule
{

}
