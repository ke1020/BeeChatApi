using Ke.Tasks.Abstractions;
using Ke.Tasks.SSE.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Ke.Tasks;

/// <summary>
/// 事件缓冲服务实现（使用Channel优化）
/// </summary>
public class EventBufferService : IEventBufferService
{
    /// <summary>
    /// 日志记录器
    /// </summary>
    private readonly ILogger<EventBufferService> _logger;
    /// <summary>
    /// 事件缓冲配置
    /// </summary>
    private readonly EventBufferOptions _options;
    /// <summary>
    /// 使用 Channel 作为事件缓冲区（生产者-消费者模式）
    /// </summary>
    private readonly Channel<SseEvent> _eventChannel;
    /// <summary>
    /// 客户端字典
    /// </summary>
    private readonly ConcurrentDictionary<string, SseClient> _clients = new();
    /// <summary>
    /// 事件字典（用于快速查找）
    /// </summary>
    private readonly ConcurrentDictionary<string, SseEvent> _eventStore = new();
    /// <summary>
    /// 事件顺序列表（用于排序和分页）
    /// </summary>
    private readonly ConcurrentDictionary<string, DateTime> _eventTimestamps = new();
    /// <summary>
    /// 清理计时器
    /// </summary>
    private readonly Timer? _cleanupTimer;
    /// <summary>
    /// 是否已释放
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// 事件流用于通知新事件（广播给所有监听器）
    /// </summary>
    private readonly Channel<SseEvent> _eventStream;
    private readonly CancellationTokenSource _eventStreamCts = new();

    public EventBufferService(ILogger<EventBufferService> logger,
        IOptions<EventBufferOptions>? options = null)
    {
        _logger = logger;
        _options = options?.Value ?? new EventBufferOptions();

        // 创建事件通道（有界通道，避免内存爆炸）
        _eventChannel = Channel.CreateBounded<SseEvent>(new BoundedChannelOptions(_options.MaxBufferSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest, // 缓冲区满时丢弃最旧的事件
            SingleWriter = false, // 允许多个写入者
            SingleReader = false  // 允许多个读取者
        });

        // 创建事件流通道（用于广播）
        _eventStream = Channel.CreateUnbounded<SseEvent>(new UnboundedChannelOptions
        {
            SingleWriter = true,  // 单个写入者（从_eventChannel读取）
            SingleReader = false  // 多个读取者（所有客户端监听）
        });

        // 启动事件处理任务
        _ = ProcessEventsAsync(_eventStreamCts.Token);
        _ = BroadcastEventsAsync(_eventStreamCts.Token);

        // 初始化清理计时器
        if (_options.EnableAutoCleanup && _options.CleanupIntervalInMinutes > 0)
        {
            _cleanupTimer = new Timer(
                _ => _ = CleanupAsync(),
                null,
                TimeSpan.FromMinutes(_options.CleanupIntervalInMinutes),
                TimeSpan.FromMinutes(_options.CleanupIntervalInMinutes)
            );
            _logger.LogDebug("自动清理计时器已启动");
        }
    }

    /// <summary>
    /// 添加事件
    /// </summary>
    /// <param name="sseEvent"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async ValueTask AddEventAsync(SseEvent sseEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sseEvent);

        // 通过Channel写入事件
        await _eventChannel.Writer.WriteAsync(sseEvent, cancellationToken);

        _logger.LogDebug("事件已加入队列: {EventId}", sseEvent.Id);
    }

    /// <summary>
    /// 获取从指定事件 ID 之后的事件
    /// </summary>
    /// <param name="lastEventId"></param>
    /// <param name="maxCount"></param>
    /// <returns></returns>
    public async Task<IEnumerable<SseEvent>> GetEventsSinceAsync(string? lastEventId, 
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var count = _options.MaxEventsPerRequest;

        if (string.IsNullOrEmpty(lastEventId))
        {
            // 返回最近的事件
            return [.. _eventTimestamps
                .OrderByDescending(kv => kv.Value)
                .Take(count)
                .Select(kv => _eventStore.TryGetValue(kv.Key, out var evt) ? evt : null)
                .Where(e => e != null)
                .Cast<SseEvent>()
                .OrderBy(e => e.Timestamp)];
        }

        // 获取指定事件之后的事件
        if (_eventTimestamps.TryGetValue(lastEventId, out var lastTimestamp))
        {
            return [.. _eventTimestamps
                .Where(kv => kv.Value > lastTimestamp)
                .OrderBy(kv => kv.Value)
                .Take(count)
                .Select(kv => _eventStore.TryGetValue(kv.Key, out var evt) ? evt : null)
                .Where(e => e != null)
                .Cast<SseEvent>()];
        }

        // 如果没有找到指定事件，返回最近的事件
        return [.. _eventTimestamps
            .OrderByDescending(kv => kv.Value)
            .Take(count)
            .Select(kv => _eventStore.TryGetValue(kv.Key, out var evt) ? evt : null)
            .Where(e => e != null)
            .Cast<SseEvent>()
            .OrderBy(e => e.Timestamp)];
    }

    /// <summary>
    /// 获取事件流（长连接模式）
    /// </summary>
    /// <param name="lastEventId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async IAsyncEnumerable<SseEvent> GetEventStreamAsync(string? lastEventId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 先发送历史事件
        var historicalEvents = await GetEventsSinceAsync(lastEventId, cancellationToken);
        foreach (var evt in historicalEvents)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            yield return evt;
        }

        // 然后监听新事件
        await foreach (var evt in _eventStream.Reader.ReadAllAsync(cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            yield return evt;
        }
    }

    /// <summary>
    /// 添加客户端
    /// </summary>
    /// <param name="clientId"></param>
    /// <param name="lastEventId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public ValueTask AddClientAsync(string clientId, string? lastEventId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(clientId))
            throw new ArgumentException("ClientId cannot be null or empty", nameof(clientId));

        var client = new SseClient(clientId)
        {
            LastEventId = lastEventId ?? string.Empty,
            LastActivityAt = DateTime.UtcNow
        };

        if (_clients.TryAdd(clientId, client))
        {
            _logger.LogInformation("客户端已连接: {ClientId}", clientId);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 移除客户端
    /// </summary>
    /// <param name="clientId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public ValueTask<bool> RemoveClientAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (_clients.TryRemove(clientId, out var client))
        {
            _logger.LogInformation("客户端已断开: {ClientId}", clientId);
            return ValueTask.FromResult(true);
        }
        return ValueTask.FromResult(false);
    }

    /// <summary>
    /// 更新客户端最后事件 ID
    /// </summary>
    /// <param name="clientId"></param>
    /// <param name="lastEventId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public ValueTask<bool> UpdateClientLastEventIdAsync(string clientId, string lastEventId, CancellationToken cancellationToken = default)
    {
        if (_clients.TryGetValue(clientId, out var client))
        {
            client.LastEventId = lastEventId;
            client.LastActivityAt = DateTime.UtcNow;
            return ValueTask.FromResult(true);
        }
        return ValueTask.FromResult(false);
    }

    /// <summary>
    /// 获取已连接客户端
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task<IEnumerable<SseClient>> GetConnectedClientsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<SseClient>>([.. _clients.Values]);
    }

    /// <summary>
    /// 清理过期事件
    /// </summary>
    /// <param name="maxAgeInMinutes"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async ValueTask ClearOldEventsAsync(int? maxAgeInMinutes = null, CancellationToken cancellationToken = default)
    {
        var age = maxAgeInMinutes ?? _options.DefaultEventMaxAgeInMinutes;
        var cutoffTime = DateTime.UtcNow.AddMinutes(-age);

        var oldEventIds = _eventTimestamps
            .Where(kv => kv.Value < cutoffTime)
            .Select(kv => kv.Key)
            .ToList();

        int removedCount = 0;
        foreach (var eventId in oldEventIds)
        {
            if (_eventStore.TryRemove(eventId, out _) && _eventTimestamps.TryRemove(eventId, out _))
            {
                removedCount++;
            }
        }

        if (removedCount > 0)
        {
            _logger.LogInformation("已清理过期事件: {Count}", removedCount);
        }
    }

    /// <summary>
    /// 清理闲置客户端
    /// </summary>
    /// <param name="maxInactiveMinutes"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async ValueTask ClearInactiveClientsAsync(int? maxInactiveMinutes = null,
        CancellationToken cancellationToken = default)
    {
        var timeout = maxInactiveMinutes ?? _options.ClientInactiveTimeoutInMinutes;
        var cutoffTime = DateTime.UtcNow.AddMinutes(-timeout);

        var inactiveClients = _clients
            .Where(kv => kv.Value.LastActivityAt < cutoffTime)
            .Select(kv => kv.Key)
            .ToList();

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
            _logger.LogInformation("已清理闲置客户端: {Count}", removedCount);
        }
    }

    /// <summary>
    /// 获取统计信息
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<EventBufferStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(new EventBufferStatistics
        {
            TotalEvents = _eventStore.Count,
            TotalClients = _clients.Count,
            OldestEventTime = !_eventTimestamps.IsEmpty
                ? _eventTimestamps.Values.Min()
                : DateTime.UtcNow,
            NewestEventTime = !_eventTimestamps.IsEmpty
                ? _eventTimestamps.Values.Max()
                : DateTime.UtcNow
        });
    }

    public int GetClientCount() => _clients.Count;
    public int GetEventCount() => _eventStore.Count;

    /// <summary>
    /// 异步清理任务
    /// </summary>
    private async Task CleanupAsync()
    {
        try
        {
            _logger.LogDebug("执行定期清理...");
            await ClearOldEventsAsync();
            await ClearInactiveClientsAsync();
            _logger.LogDebug("定期清理完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "定期清理过程中发生错误");
        }
    }

    /// <summary>
    /// 处理事件并存储到内存中
    /// </summary>
    private async Task ProcessEventsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var sseEvent in _eventChannel.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    // 存储事件
                    StoreEvent(sseEvent);

                    // 广播事件
                    await _eventStream.Writer.WriteAsync(sseEvent, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理事件时发生错误: {EventId}", sseEvent.Id);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常退出
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "事件处理任务发生错误");
        }
    }

    /// <summary>
    /// 广播事件给所有客户端（如果需要）
    /// 实际应用中，这个可以根据需要实现，比如客户端连接时获取自己的事件流
    /// </summary>
    private async Task BroadcastEventsAsync(CancellationToken cancellationToken)
    {
        // 这里可以维护客户端特定的通道，实现定向推送
        // 当前实现是统一的广播流
        // 实际应用中，可以为每个客户端创建独立的Channel
    }

    /// <summary>
    /// 存储事件到内存字典
    /// </summary>
    private void StoreEvent(SseEvent sseEvent)
    {
        if (string.IsNullOrEmpty(sseEvent.Id))
            sseEvent.Id = Guid.NewGuid().ToString();

        if (sseEvent.Timestamp.Kind != DateTimeKind.Utc)
            sseEvent.Timestamp = sseEvent.Timestamp.ToUniversalTime();

        // 添加到存储
        if (_eventStore.TryAdd(sseEvent.Id, sseEvent))
        {
            _eventTimestamps.TryAdd(sseEvent.Id, sseEvent.Timestamp);

            // 维护存储大小
            if (_eventStore.Count > _options.MaxBufferSize)
            {
                RemoveOldestEvent();
            }

            _logger.LogDebug("事件已存储: {EventId}", sseEvent.Id);
        }
    }

    /// <summary>
    /// 移除最旧的事件
    /// </summary>
    private void RemoveOldestEvent()
    {
        var oldest = _eventTimestamps.OrderBy(kv => kv.Value).FirstOrDefault();
        if (!string.IsNullOrEmpty(oldest.Key))
        {
            _eventStore.TryRemove(oldest.Key, out _);
            _eventTimestamps.TryRemove(oldest.Key, out _);
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _eventStreamCts.Cancel();
            _cleanupTimer?.Dispose();
            _eventStream.Writer.TryComplete();
            _eventChannel.Writer.TryComplete();
            _eventStreamCts.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}