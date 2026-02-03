namespace Ke.Tasks.Models;

public class TaskNotificationRequest
{
    public Guid TaskId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string? LastEventId { get; set; }
}

public class SpeechRecognitionNotificationRequest : TaskNotificationRequest
{

    public List<string> FilePaths { get; set; } = [];
}