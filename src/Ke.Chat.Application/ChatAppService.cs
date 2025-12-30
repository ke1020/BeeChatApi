using Ke.Chat.Localization;
using Volo.Abp.Application.Services;

namespace Ke.Chat;

public abstract class ChatAppService : ApplicationService
{
    protected ChatAppService()
    {
        LocalizationResource = typeof(ChatResource);
        ObjectMapperContext = typeof(ChatApplicationModule);
    }
}
