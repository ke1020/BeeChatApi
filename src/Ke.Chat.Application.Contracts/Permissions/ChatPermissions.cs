using Volo.Abp.Reflection;

namespace Ke.Chat.Permissions;

public class ChatPermissions
{
    public const string GroupName = "Chat";

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(ChatPermissions));
    }
}
