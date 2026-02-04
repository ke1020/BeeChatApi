using System.Threading.Channels;
using Ke.Tasks.SSE.Models;

namespace Ke.Tasks;

public static class ChannelWriterExtensions
{
    /// <summary>
    /// 写入 Ready 事件
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="requestMessageId"></param>
    /// <param name="responseMessageId"></param>
    /// <param name="eventType"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static ValueTask WriteReadyAsync(this ChannelWriter<SseEvent> writer,
        int requestMessageId,
        int responseMessageId,
        string eventType = "ready",
        CancellationToken cancellationToken = default)
    {
        return writer.WriteAsync(
            new ReadyEvent(requestMessageId, responseMessageId, eventType),
            cancellationToken
        );
    }

    /// <summary>
    /// 写入进度事件
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="progress"></param>
    /// <param name="message"></param>
    /// <param name="eventType"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static ValueTask WriteProgressAsync(this ChannelWriter<SseEvent> writer,
        int progress,
        string? message = null,
        string eventType = "progress",
        CancellationToken cancellationToken = default)
    {
        return writer.WriteAsync(
            new TaskProgressEvent(progress, eventType)
            {
            },
            cancellationToken)
            ;
    }

    /// <summary>
    /// 写入错误事件
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="error"></param>
    /// <param name="eventType"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static ValueTask WriteErrorAsync(
        this ChannelWriter<SseEvent> writer,
        string error,
        string eventType = "error",
        CancellationToken cancellationToken = default)
    {
        return writer.WriteAsync(new TaskErrorEvent(error, eventType),
            cancellationToken)
            ;
    }

    /// <summary>
    /// 写入数据事件
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="writer"></param>
    /// <param name="data"></param>
    /// <param name="eventType"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static ValueTask WriteDataAsync<T>(
        this ChannelWriter<SseEvent> writer,
        T data,
        string eventType = "data",
        CancellationToken cancellationToken = default)
    {
        return writer.WriteAsync(new DataEvent<T>(data, eventType),
            cancellationToken)
            ;
    }

    /// <summary>
    /// 写入完成事件
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="result"></param>
    /// <param name="eventType"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static ValueTask WriteCompleteAsync(this ChannelWriter<SseEvent> writer,
        string? result = null,
        string eventType = "completed",
        CancellationToken cancellationToken = default)
    {
        return writer.WriteAsync(new TaskCompletedEvent(eventType)
        {
            Result = result
        }, cancellationToken)
        ;
    }
}