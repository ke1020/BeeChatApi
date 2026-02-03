using Ke.Tasks.SSE.Models;

namespace Ke.Tasks.Abstractions;

/// <summary>
/// 事件缓冲服务接口
/// </summary>
public interface IEventBufferService : IDisposable
{
    /// <summary>
    /// 添加事件
    /// </summary>
    /// <param name="sseEvent"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask AddEventAsync(SseEvent sseEvent, 
        CancellationToken cancellationToken = default);
    /// <summary>
    /// 获取从指定事件 ID 之后的事件
    /// </summary>
    /// <param name="lastEventId"></param>
    /// <param name="maxCount"></param>
    /// <returns></returns>
    Task<IEnumerable<SseEvent>> GetEventsSinceAsync(string? lastEventId, 
        CancellationToken cancellationToken = default);
    /// <summary>
    /// 获取事件流（长连接模式）
    /// </summary>
    /// <param name="lastEventId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    IAsyncEnumerable<SseEvent> GetEventStreamAsync(string? lastEventId = null,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// 添加客户端
    /// </summary>
    /// <param name="clientId"></param>
    /// <param name="lastEventId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    ValueTask AddClientAsync(string clientId, 
        string? lastEventId = null, 
        CancellationToken cancellationToken = default);
    /// <summary>
    /// 移除客户端
    /// </summary>
    /// <param name="clientId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask<bool> RemoveClientAsync(string clientId, 
        CancellationToken cancellationToken = default);
    /// <summary>
    /// 更新客户端最后事件 ID
    /// </summary>
    /// <param name="clientId"></param>
    /// <param name="lastEventId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask<bool> UpdateClientLastEventIdAsync(string clientId, 
        string lastEventId, 
        CancellationToken cancellationToken = default);
    /// <summary>
    /// 获取已连接客户端
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IEnumerable<SseClient>> GetConnectedClientsAsync(
        CancellationToken cancellationToken = default);
    /// <summary>
    /// 清理过期事件
    /// </summary>
    /// <param name="maxAgeInMinutes"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask ClearOldEventsAsync(int? maxAgeInMinutes = null, 
        CancellationToken cancellationToken = default);
    /// <summary>
    /// 清理闲置客户端
    /// </summary>
    /// <param name="maxInactiveMinutes"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask ClearInactiveClientsAsync(int? maxInactiveMinutes = null,
        CancellationToken cancellationToken = default);
}