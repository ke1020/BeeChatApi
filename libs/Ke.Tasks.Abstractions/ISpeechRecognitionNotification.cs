using Ke.Tasks.Models;

namespace Ke.Tasks.Abstractions;

/// <summary>
/// 语音识别任务通知接口
/// </summary>
public interface ISpeechRecognitionNotification 
    : ITaskNotification<SpeechRecognitionNotificationRequest>
{
    
}