namespace Ke.Tasks.Models;

public class TaskNotificationRequest
{
    public string TaskName { get; set; } = string.Empty;
    public string? LastEventId { get; set; }
}

public class SpeechRecognitionNotificationRequest : TaskNotificationRequest
{

    public List<string> FilePaths { get; set; } = [];
}