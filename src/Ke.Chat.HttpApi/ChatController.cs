using Ke.Chat.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Ke.Chat;

public abstract class ChatController : AbpControllerBase
{
    protected ChatController()
    {
        LocalizationResource = typeof(ChatResource);
    }
}
