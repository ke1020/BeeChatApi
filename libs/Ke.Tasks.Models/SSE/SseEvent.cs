using System.Text.Json.Serialization;

namespace Ke.Tasks.SSE.Models;

/// <summary>
/// SSE 事件
/// </summary>
/// <param name="eventType"></param>
/// <param name="Timestamp"></param>
public class SseEvent(string eventType)
{
    [JsonPropertyName("event")]
    public string EventType { get; } = eventType;

}

/// <summary>
/// SSE 缓冲区数据
/// </summary>
/// <param name="eventType"></param>
public sealed class SseEventBufferData(string id, string eventType) : SseEvent(eventType)
{
    public string Id { get; set; } = id;
    public long Timestamp { get; set; } = DateTimeOffset.Now.ToUnixTimeMilliseconds();
}

/// <summary>
/// 就绪事件
/// </summary>
/// <param name="RequestMessageId"></param>
/// <param name="ResponseMessageId"></param>
/// <param name="eventType"></param>
public sealed class ReadyEvent(int requestMessageId, int responseMessageId, string eventType = "ready")
    : SseEvent(eventType)
{
    public int RequestMessageId { get; } = requestMessageId;
    public int ResponseMessageId { get; } = responseMessageId;
}

/// <summary>
/// 进度事件
/// </summary>
/// <param name="progress"></param>
/// <param name="eventType"></param>
public class TaskProgressEvent(double progress, string eventType = "progress")
    : SseEvent(eventType)
{
    public double Progress { get; } = progress;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? FileIndex { get; set; }
}

/// <summary>
/// 任务完成事件
/// </summary>
/// <param name="eventType"></param>
public sealed class TaskCompletedEvent(string eventType = "completed")
    : SseEvent(eventType)
{
    public string? Result { get; set; }
}

/// <summary>
/// 任务失败事件
/// </summary>
/// <param name="error"></param>
/// <param name="eventType"></param>
public sealed class TaskErrorEvent(string error, string eventType = "error")
    : SseEvent(eventType)
{
    public string Error { get; } = error;
}


/// <summary>
/// 数据事件
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="data"></param>
/// <param name="eventType"></param>
public sealed class DataEvent<T>(T data, string eventType = "data")
    : SseEvent(eventType)
{
    public T Data { get; } = data;
}