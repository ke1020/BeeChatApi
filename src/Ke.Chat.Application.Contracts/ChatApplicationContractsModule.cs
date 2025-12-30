using Volo.Abp.Application;
using Volo.Abp.Modularity;
using Volo.Abp.Authorization;

namespace Ke.Chat;

[DependsOn(
    typeof(ChatDomainSharedModule),
    typeof(AbpDddApplicationContractsModule),
    typeof(AbpAuthorizationModule)
    )]
public class ChatApplicationContractsModule : AbpModule
{

}
