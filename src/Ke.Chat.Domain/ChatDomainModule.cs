using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Ke.Chat;

[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(ChatDomainSharedModule)
)]
public class ChatDomainModule : AbpModule
{

}
