namespace Ke.Tasks.Models.Chats;

public class ChatResponse
{
    public int Code { get; set; }
    public string? Message { get; set; }
    public HistoryMessage HistoryMessage { get; set; }
}

public class HistoryMessage
{
    public ChatRequest ChatSession { get; set; }
    public List<ChatMessageInputDto> Messages { get; set; } = [];
}