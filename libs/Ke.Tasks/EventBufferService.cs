using System.Collections.Concurrent;
using Ke.Tasks.Abstractions;
using Ke.Tasks.SSE.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ke.Tasks;

/// <summary>
/// 事件缓冲服务实现
/// </summary>
public class EventBufferService : IEventBufferService, IDisposable
{
    /// <summary>
    /// 日志记录器
    /// </summary>
    private readonly ILogger<EventBufferService> _logger;
    /// <summary>
    /// 事件缓冲区配置项
    /// </summary>
    private readonly EventBufferOptions _options;
    /// <summary>
    /// 事件缓冲区，Key: EventId, Value: 事件
    /// </summary>
    private readonly ConcurrentDictionary<string, SseEvent> _events = new();
    /// <summary>
    /// 已连接客户端，Key: ClientId, Value: 客户端信息
    /// </summary>
    private readonly ConcurrentDictionary<string, SseClient> _clients = new();
    /// <summary>
    /// 事件顺序队列，用于维护事件的新旧顺序
    /// </summary>
    private readonly ConcurrentQueue<string> _eventQueue = new();
    /// <summary>
    /// 事件ID索引，用于快速查找事件位置
    /// </summary>
    private readonly ConcurrentDictionary<string, long> _eventTimestamps = new();
    /// <summary>
    /// 清理过期事件的计时器
    /// </summary>
    private readonly Timer? _cleanupTimer;
    /// <summary>
    /// 用于读写同步的信号量（支持异步）
    /// 初始计数：1（允许一个线程进入），最大计数：1
    /// </summary>
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    /// <summary>
    /// 读取信号量，允许多个读取者，但写入时需要独占
    /// </summary>
    private readonly ReaderWriterLockSlim _rwLock = new(LockRecursionPolicy.NoRecursion);
    /// <summary>
    /// 是否已释放资源
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 构造函数
    /// </summary>
    public EventBufferService(ILogger<EventBufferService> logger,
        IOptions<EventBufferOptions>? options = null)
    {
        _logger = logger;
        _options = options?.Value ?? new EventBufferOptions();

        // 初始化清理计时器（如果需要）
        if (_options.EnableAutoCleanup && _options.CleanupIntervalInMinutes > 0)
        {
            _cleanupTimer = new Timer(
                CleanupCallback,
                null,
                TimeSpan.FromMinutes(_options.CleanupIntervalInMinutes),
                TimeSpan.FromMinutes(_options.CleanupIntervalInMinutes)
            );

            _logger.LogDebug("自动清理计时器已启动，间隔: {Interval}分钟",
                _options.CleanupIntervalInMinutes)
                ;
        }
    }

    /// <summary>
    /// 清理回调方法
    /// </summary>
    private async void CleanupCallback(object? state)
    {
        try
        {
            _logger.LogDebug("执行定期清理...");

            // 异步执行清理操作
            await Task.Run(async () =>
            {
                // 清理过期事件
                await ClearOldEventsAsync(_options.DefaultEventMaxAgeInMinutes);

                // 清理闲置客户端
                await ClearInactiveClientsAsync(_options.ClientInactiveTimeoutInMinutes);
            });

            _logger.LogDebug("定期清理完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "定期清理过程中发生错误");
        }
    }

    /// <summary>
    /// 添加事件到缓冲区
    /// </summary>
    public void AddEvent(SseEvent sseEvent)
    {
        ArgumentNullException.ThrowIfNull(sseEvent);

        if (string.IsNullOrEmpty(sseEvent.Id))
        {
            sseEvent.Id = Guid.NewGuid().ToString();
        }

        // 确保时间戳是UTC时间
        if (sseEvent.Timestamp.Kind != DateTimeKind.Utc)
        {
            sseEvent.Timestamp = sseEvent.Timestamp.ToUniversalTime();
        }

        // 使用写入锁（独占访问）
        _rwLock.EnterWriteLock();
        try
        {
            // 添加到字典
            if (_events.TryAdd(sseEvent.Id, sseEvent))
            {
                // 添加到队列
                _eventQueue.Enqueue(sseEvent.Id);

                // 添加到时间戳索引
                _eventTimestamps.TryAdd(sseEvent.Id, sseEvent.Timestamp.Ticks);

                // 保持缓冲区大小
                TrimBuffer();
            }
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }

        _logger.LogDebug("事件已添加到缓冲区: {EventId}, 类型: {EventType}, 当前缓冲区大小: {Count}",
            sseEvent.Id, sseEvent.EventType, _events.Count)
            ;
    }

    /// <summary>
    /// 异步添加事件到缓冲区
    /// </summary>
    public async Task AddEventAsync(SseEvent sseEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sseEvent);

        if (string.IsNullOrEmpty(sseEvent.Id))
        {
            sseEvent.Id = Guid.NewGuid().ToString();
        }

        // 确保时间戳是UTC时间
        if (sseEvent.Timestamp.Kind != DateTimeKind.Utc)
        {
            sseEvent.Timestamp = sseEvent.Timestamp.ToUniversalTime();
        }

        // 使用信号量进行异步等待
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            // 添加到字典
            if (_events.TryAdd(sseEvent.Id, sseEvent))
            {
                // 添加到队列
                _eventQueue.Enqueue(sseEvent.Id);

                // 添加到时间戳索引
                _eventTimestamps.TryAdd(sseEvent.Id, sseEvent.Timestamp.Ticks);

                // 保持缓冲区大小
                TrimBuffer();
            }
        }
        finally
        {
            _semaphore.Release();
        }

        _logger.LogDebug("事件已异步添加到缓冲区: {EventId}, 类型: {EventType}, 当前缓冲区大小: {Count}",
            sseEvent.Id, sseEvent.EventType, _events.Count)
            ;
    }

    /// <summary>
    /// 获取从 lastEventId 之后的事件
    /// </summary>
    public IEnumerable<SseEvent> GetEventsSince(string? lastEventId)
    {
        if (string.IsNullOrEmpty(lastEventId) || !_events.ContainsKey(lastEventId))
        {
            // 使用读取锁（共享访问）
            _rwLock.EnterReadLock();
            try
            {
                return GetRecentEvents(_options.DefaultEventCount);
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        // 使用读取锁（共享访问）
        _rwLock.EnterReadLock();
        try
        {
            // 获取所有事件并按时间排序
            var orderedEvents = GetOrderedEvents();

            // 使用二分查找提高性能
            var index = FindEventIndex(orderedEvents, lastEventId);
            if (index >= 0 && index < orderedEvents.Count - 1)
            {
                // 返回指定数量的事件，避免一次返回太多数据
                return orderedEvents
                    .Skip(index + 1)
                    .Take(_options.MaxEventsPerRequest)
                    ;
            }

            return [];
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <summary>
    /// 异步获取从 lastEventId 之后的事件
    /// </summary>
    public async Task<IEnumerable<SseEvent>> GetEventsSinceAsync(string? lastEventId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(lastEventId) || !_events.ContainsKey(lastEventId))
        {
            // 使用信号量进行异步等待
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                return GetRecentEvents(_options.DefaultEventCount);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        // 使用信号量进行异步等待
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            // 获取所有事件并按时间排序
            var orderedEvents = GetOrderedEvents();

            // 使用二分查找提高性能
            var index = FindEventIndex(orderedEvents, lastEventId);
            if (index >= 0 && index < orderedEvents.Count - 1)
            {
                // 返回指定数量的事件，避免一次返回太多数据
                return orderedEvents
                    .Skip(index + 1)
                    .Take(_options.MaxEventsPerRequest)
                    ;
            }

            return [];
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 获取所有事件（按时间排序）
    /// </summary>
    private List<SseEvent> GetOrderedEvents()
    {
        // 如果事件数量较少，直接排序
        if (_events.Count <= _options.SortThreshold)
        {
            return [.. _events.Values
                .OrderBy(e => e.Timestamp)
                .ThenBy(e => e.Id)]
                ;
        }

        // 使用时间戳索引进行排序，提高性能
        return [.. _eventTimestamps
            .OrderBy(kv => kv.Value)
            .Select(kv => _events.TryGetValue(kv.Key, out var evt) ? evt : null)
            .Where(e => e != null)
            .Cast<SseEvent>()]
            ;
    }

    /// <summary>
    /// 查找事件索引
    /// </summary>
    private static int FindEventIndex(List<SseEvent> orderedEvents, string lastEventId)
    {
        var comparer = Comparer<SseEvent>.Create((a, b) =>
        {
            var timeCompare = a.Timestamp.CompareTo(b.Timestamp);
            return timeCompare != 0 ? timeCompare : string.Compare(a.Id, b.Id, StringComparison.Ordinal);
        });

        var dummyEvent = new SseEvent { Id = lastEventId };
        var index = orderedEvents.BinarySearch(dummyEvent, comparer);

        return index >= 0 ? index : -1;
    }

    /// <summary>
    /// 获取最近的事件
    /// </summary>
    private IEnumerable<SseEvent> GetRecentEvents(int count)
    {
        // 从队列尾部开始获取最新的count个事件
        var recentEventIds = _eventQueue
            .Reverse()
            .Where(id => _events.ContainsKey(id))
            .Take(count)
            .ToList()
            ;

        return recentEventIds
            .Select(id => _events.TryGetValue(id, out var evt) ? evt : null)
            .Where(e => e != null)
            .Cast<SseEvent>()
            .OrderBy(e => e.Timestamp)
            ;
    }

    /// <summary>
    /// 清理缓冲区，保持大小限制
    /// </summary>
    private void TrimBuffer()
    {
        while (_eventQueue.Count > _options.MaxBufferSize && _eventQueue.TryDequeue(out var oldId))
        {
            _events.TryRemove(oldId, out _);
            _eventTimestamps.TryRemove(oldId, out _);
        }
    }

    /// <summary>
    /// 清理过期事件
    /// </summary>
    public void ClearOldEvents(int? maxAgeInMinutes = null)
    {
        var age = maxAgeInMinutes ?? _options.DefaultEventMaxAgeInMinutes;
        var cutoffTime = DateTime.UtcNow.AddMinutes(-age);

        // 使用写入锁（独占访问）
        _rwLock.EnterWriteLock();
        try
        {
            // 使用时间戳索引查找过期事件
            var oldEventIds = _eventTimestamps
                .Where(kv => new DateTime(kv.Value) < cutoffTime)
                .Select(kv => kv.Key)
                .ToList()
                ;

            if (oldEventIds.Count == 0)
                return;

            int removedCount = 0;
            foreach (var eventId in oldEventIds)
            {
                if (_events.TryRemove(eventId, out _))
                {
                    _eventTimestamps.TryRemove(eventId, out _);
                    removedCount++;
                }
            }

            // 重建队列（移除已删除的事件）
            if (removedCount > 0)
            {
                RebuildEventQueue();
            }

            _logger.LogInformation("已清理 {RemovedCount} 个过期事件（超过 {Age} 分钟）",
                removedCount, age)
                ;
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// 异步清理过期事件
    /// </summary>
    public async Task ClearOldEventsAsync(int? maxAgeInMinutes = null, CancellationToken cancellationToken = default)
    {
        var age = maxAgeInMinutes ?? _options.DefaultEventMaxAgeInMinutes;
        var cutoffTime = DateTime.UtcNow.AddMinutes(-age);

        // 使用信号量进行异步等待
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            // 使用时间戳索引查找过期事件
            var oldEventIds = _eventTimestamps
                .Where(kv => new DateTime(kv.Value) < cutoffTime)
                .Select(kv => kv.Key)
                .ToList()
                ;

            if (oldEventIds.Count == 0)
                return;

            int removedCount = 0;
            foreach (var eventId in oldEventIds)
            {
                if (_events.TryRemove(eventId, out _))
                {
                    _eventTimestamps.TryRemove(eventId, out _);
                    removedCount++;
                }
            }

            // 重建队列（移除已删除的事件）
            if (removedCount > 0)
            {
                RebuildEventQueue();
            }

            _logger.LogInformation("已异步清理 {RemovedCount} 个过期事件（超过 {Age} 分钟）",
                removedCount, age)
                ;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 重建事件队列
    /// </summary>
    private void RebuildEventQueue()
    {
        var newQueue = new ConcurrentQueue<string>();

        // 按时间顺序重新填充队列
        foreach (var kv in _eventTimestamps.OrderBy(kv => kv.Value))
        {
            newQueue.Enqueue(kv.Key);
        }

        // 清空并重新填充队列
        while (_eventQueue.TryDequeue(out _)) { }
        foreach (var item in newQueue)
        {
            _eventQueue.Enqueue(item);
        }
    }

    /// <summary>
    /// 添加客户端
    /// </summary>
    public void AddClient(string clientId, string? lastEventId = null)
    {
        if (string.IsNullOrEmpty(clientId))
            throw new ArgumentException("ClientId cannot be null or empty", nameof(clientId));

        var client = new SseClient(clientId)
        {
            LastEventId = lastEventId ?? string.Empty,
            LastActivityAt = DateTime.UtcNow
        };

        // 客户端管理使用轻量级同步，不需要强一致性锁
        if (_clients.TryAdd(clientId, client))
        {
            _logger.LogInformation("客户端已连接: {ClientId}, LastEventId: {LastEventId}, 当前客户端数: {ClientCount}",
                clientId, lastEventId, _clients.Count)
                ;
        }
        else
        {
            _logger.LogWarning("客户端已存在: {ClientId}", clientId);
        }
    }

    /// <summary>
    /// 异步添加客户端
    /// </summary>
    public Task AddClientAsync(string clientId, string? lastEventId = null, CancellationToken cancellationToken = default)
    {
        AddClient(clientId, lastEventId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 移除客户端
    /// </summary>
    public bool RemoveClient(string clientId)
    {
        if (_clients.TryRemove(clientId, out var client))
        {
            _logger.LogInformation("客户端已断开: {ClientId}, 连接时长: {Duration:F1}秒",
                clientId, (DateTime.UtcNow - client.ConnectedAt).TotalSeconds);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 异步移除客户端
    /// </summary>
    public Task<bool> RemoveClientAsync(string clientId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(RemoveClient(clientId));
    }

    /// <summary>
    /// 更新客户端的 LastEventId
    /// </summary>
    public bool UpdateClientLastEventId(string clientId, string lastEventId)
    {
        if (_clients.TryGetValue(clientId, out var client))
        {
            client.LastEventId = lastEventId;
            client.LastActivityAt = DateTime.UtcNow;

            _logger.LogDebug("更新客户端 {ClientId} 的 LastEventId: {LastEventId}",
                clientId, lastEventId)
                ;
            return true;
        }

        _logger.LogWarning("尝试更新不存在的客户端: {ClientId}", clientId);
        return false;
    }

    /// <summary>
    /// 异步更新客户端的 LastEventId
    /// </summary>
    public Task<bool> UpdateClientLastEventIdAsync(string clientId, string lastEventId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(UpdateClientLastEventId(clientId, lastEventId));
    }

    /// <summary>
    /// 获取已连接客户端
    /// </summary>
    public IEnumerable<SseClient> GetConnectedClients() => _clients.Values;

    /// <summary>
    /// 异步获取已连接客户端
    /// </summary>
    public Task<IEnumerable<SseClient>> GetConnectedClientsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetConnectedClients());
    }

    /// <summary>
    /// 获取客户端数量
    /// </summary>
    public int GetClientCount() => _clients.Count;

    /// <summary>
    /// 获取事件数量
    /// </summary>
    public int GetEventCount() => _events.Count;

    /// <summary>
    /// 清理闲置客户端
    /// </summary>
    public void ClearInactiveClients(int? maxInactiveMinutes = null)
    {
        var timeout = maxInactiveMinutes ?? _options.ClientInactiveTimeoutInMinutes;
        var cutoffTime = DateTime.UtcNow.AddMinutes(-timeout);

        var inactiveClients = _clients
            .Where(kv => kv.Value.LastActivityAt < cutoffTime)
            .Select(kv => kv.Key)
            .ToList()
            ;

        int removedCount = 0;
        foreach (var clientId in inactiveClients)
        {
            if (RemoveClient(clientId))
            {
                removedCount++;
            }
        }

        if (removedCount > 0)
        {
            _logger.LogInformation("已清理 {Count} 个闲置客户端（超过 {Timeout} 分钟无活动）",
                removedCount, timeout)
                ;
        }
    }

    /// <summary>
    /// 异步清理闲置客户端
    /// </summary>
    public async Task ClearInactiveClientsAsync(int? maxInactiveMinutes = null, CancellationToken cancellationToken = default)
    {
        var timeout = maxInactiveMinutes ?? _options.ClientInactiveTimeoutInMinutes;
        var cutoffTime = DateTime.UtcNow.AddMinutes(-timeout);

        var inactiveClients = _clients
            .Where(kv => kv.Value.LastActivityAt < cutoffTime)
            .Select(kv => kv.Key)
            .ToList()
            ;

        int removedCount = 0;
        foreach (var clientId in inactiveClients)
        {
            if (await RemoveClientAsync(clientId, cancellationToken))
            {
                removedCount++;
            }
        }

        if (removedCount > 0)
        {
            _logger.LogInformation("已异步清理 {Count} 个闲置客户端（超过 {Timeout} 分钟无活动）",
                removedCount, timeout)
                ;
        }
    }

    /// <summary>
    /// 获取服务统计信息
    /// </summary>
    public EventBufferStatistics GetStatistics()
    {
        // 使用读取锁（共享访问）
        _rwLock.EnterReadLock();
        try
        {
            return new EventBufferStatistics
            {
                TotalEvents = _events.Count,
                TotalClients = _clients.Count,
                QueueLength = _eventQueue.Count,
                OldestEventTime = !_eventTimestamps.IsEmpty
                    ? new DateTime(_eventTimestamps.Values.Min())
                    : DateTime.UtcNow,
                NewestEventTime = !_eventTimestamps.IsEmpty
                    ? new DateTime(_eventTimestamps.Values.Max())
                    : DateTime.UtcNow
            };
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <summary>
    /// 异步获取服务统计信息
    /// </summary>
    public async Task<EventBufferStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        // 使用信号量进行异步等待
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            return new EventBufferStatistics
            {
                TotalEvents = _events.Count,
                TotalClients = _clients.Count,
                QueueLength = _eventQueue.Count,
                OldestEventTime = !_eventTimestamps.IsEmpty
                    ? new DateTime(_eventTimestamps.Values.Min())
                    : DateTime.UtcNow,
                NewestEventTime = !_eventTimestamps.IsEmpty
                    ? new DateTime(_eventTimestamps.Values.Max())
                    : DateTime.UtcNow
            };
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放资源的实现
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // 清理托管资源
                _cleanupTimer?.Dispose();
                _semaphore?.Dispose();
                _rwLock?.Dispose();
            }

            _disposed = true;
        }
    }
}