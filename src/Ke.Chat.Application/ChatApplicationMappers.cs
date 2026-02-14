using Ke.Chat.Chats;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace Ke.Chat;

[Mapper]
public partial class ChatApplicationMappers
{
    /* You can configure your Mapperly mapping configuration here.
     * Alternatively, you can split your mapping configurations
     * into multiple mapper classes for a better organization. */
}

[Mapper]
public partial class TaskInfoMappers : MapperBase<Tasks.Models.TaskInfo, TaskInfo>
{
    public override partial TaskInfo Map(Tasks.Models.TaskInfo taskInfo);
    public override partial void Map(Tasks.Models.TaskInfo source, TaskInfo destination);
}