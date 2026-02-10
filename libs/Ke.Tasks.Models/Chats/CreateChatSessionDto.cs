namespace Ke.Tasks.Models.Chats;

public class CreateChatSessionDto(Guid sessionId, string title)
{
    public Guid SessionId { get; set; } = sessionId;
    public string Title { get; set; } = title;
    public DateTime CreationTime { get; set; } = DateTime.UtcNow;
    public List<ChatMessageInputDto> Messages { get; set; } = [];
}