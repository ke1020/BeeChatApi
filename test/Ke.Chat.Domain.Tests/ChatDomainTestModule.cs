using Volo.Abp.Modularity;

namespace Ke.Chat;

[DependsOn(
    typeof(ChatDomainModule),
    typeof(ChatTestBaseModule)
)]
public class ChatDomainTestModule : AbpModule
{

}
