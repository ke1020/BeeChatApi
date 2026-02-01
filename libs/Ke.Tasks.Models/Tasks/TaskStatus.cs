namespace Ke.Tasks.Models;

public enum TaskStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelled
}

public enum FileStatus
{
    Pending,
    Preparing,
    Transcoding,
    TranscodingCompleted,
    Recognizing,
    Completed,
    Failed
}