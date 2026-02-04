using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ke.Tasks.Abstractions;
using Ke.Tasks.Models;
using Ke.Tasks.SSE.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Volo.Abp.AspNetCore.Mvc;

namespace Ke.Chat.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SseController(ILogger<SseController> logger, 
    IEventBufferService eventBuffer,
    IChat chat) : AbpController
{
    private readonly IEventBufferService _eventBuffer = eventBuffer;
    private readonly ILogger<SseController> _logger = logger;
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _clientCancellationTokens = new();
    //private readonly ISpeechRecognitionNotification _speechRecognitionNotification = speechRecognitionNotification;
    private readonly IChat _chat = chat;

    /// <summary>
    /// 带进度更新的流式任务
    /// </summary>
    [HttpGet("progress-task")]
    public ServerSentEventsResult<SseEvent> ProgressTaskStream(
        [FromQuery] string taskName = "处理任务",
        [FromHeader(Name = "Last-Event-ID")] string? lastEventId = null)
    {
        return TypedResults.ServerSentEvents(
            _chat.SendAsync(new Tasks.Models.Chats.ChatRequest
            {
                Prompt = string.Empty,
                RefFileIds = [@"C:\Users\ke\dev\proj\tools\BeeChat\ChatApi\host\Ke.Chat.HttpApi.Host\FodyWeavers.xml"],
                TaskType = "asr"
            }))
            ;
    }

    /*

    /// <summary>
    /// SSE 流式端点 - 支持重连
    /// </summary>
    /// <param name="lastEventId">上次接收的事件ID（从 Last-Event-ID 头部获取）</param>
    [HttpGet("stream")]
    public async Task<ServerSentEventsResult<SseEvent>> GetEventStream(
        [FromHeader(Name = "Last-Event-ID")] string? lastEventId = null)
    {
        var clientId = Guid.NewGuid().ToString();
        _logger.LogInformation("新SSE连接: {ClientId}, LastEventId: {LastEventId}",
            clientId, lastEventId ?? "none");

        // 注册客户端
        await _eventBuffer.AddClientAsync(clientId, lastEventId ?? string.Empty);

        // 创建客户端特定的取消令牌
        var cts = new CancellationTokenSource();
        _clientCancellationTokens[clientId] = cts;

        // 清理客户端连接
        Response.RegisterForDispose(new ClientDisposer(clientId, _clientCancellationTokens, cts, _logger));

        return TypedResults.ServerSentEvents(
            GenerateEventStream(clientId, lastEventId, cts.Token), "tasks");
    }

    /// <summary>
    /// 支持重连的聊天流
    /// </summary>
    [HttpGet("chat")]
    public ServerSentEventsResult<SseEvent> ChatStream(
        [FromQuery] string question,
        //[FromHeader(Name = "Last-Event-ID")] string? lastEventId = null)
        [FromQuery] string? lastEventId = null)
    {
        _logger.LogInformation("聊天请求: {Question}, LastEventId: {LastEventId}",
            question, lastEventId ?? "none");

        return TypedResults.ServerSentEvents(
            GenerateChatStream(question, lastEventId));
    }

    

    /// <summary>
    /// 实时数据更新流
    /// </summary>
    [HttpGet("live-data")]
    public ServerSentEventsResult<SseEvent> LiveDataStream(
        [FromQuery] string dataType = "metrics",
        [FromHeader(Name = "Last-Event-ID")] string? lastEventId = null)
    {
        return TypedResults.ServerSentEvents(
            GenerateLiveDataStream(dataType, lastEventId));
    }

    /// <summary>
    /// 获取已连接客户端信息
    /// </summary>
    [HttpGet("clients")]
    public async Task<IActionResult> GetConnectedClients()
    {
        var clients = (await _eventBuffer.GetConnectedClientsAsync())
            .Select(c => new
            {
                c.ClientId,
                c.ConnectedAt,
                c.LastEventId,
                ConnectionDuration = DateTime.UtcNow - c.ConnectedAt
            });

        return Ok(new
        {
            TotalClients = clients.Count(),
            Clients = clients
        });
    }

    /// <summary>
    /// 断开指定客户端
    /// </summary>
    [HttpPost("disconnect/{clientId}")]
    public IActionResult DisconnectClient(string clientId)
    {
        if (_clientCancellationTokens.TryRemove(clientId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();

            _logger.LogInformation("已断开客户端: {ClientId}", clientId);
            return Ok(new { Success = true, ClientId = clientId });
        }

        return NotFound(new { Error = "客户端未找到" });
    }

    

    /// <summary>
    /// 生成事件流的核心方法
    /// </summary>
    private async IAsyncEnumerable<SseEvent> GenerateEventStream(
        string clientId,
        string? lastEventId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("开始为客户端 {ClientId} 生成事件流", clientId);

            // 1. 发送连接确认事件
            yield return new SseEvent
            {
                EventType = "connected",
                Data = new
                {
                    ClientId = clientId,
                    Message = "SSE连接已建立",
                    ServerTime = DateTime.UtcNow,
                    SupportsReconnect = true
                },
                Retry = 5000 // 5秒重连间隔
            };

            // 2. 重连时发送错过的消息
            if (!string.IsNullOrEmpty(lastEventId))
            {
                _logger.LogDebug("客户端 {ClientId} 重连，发送错过的消息", clientId);

                var missedEvents = await _eventBuffer.GetEventsSinceAsync(lastEventId);
                foreach (var missedEvent in missedEvents)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    // 标记为重连补发的事件
                    var reconnectionEvent = new SseEvent
                    {
                        Id = missedEvent.Id,
                        EventType = "reconnection",
                        Data = new
                        {
                            OriginalEvent = missedEvent.Data,
                            IsCatchUp = true,
                            Message = "重连补发消息"
                        }
                    };

                    yield return reconnectionEvent;
                    await Task.Delay(100, cancellationToken); // 避免发送过快
                }

                // 发送重连完成事件
                yield return new SseEvent
                {
                    EventType = "reconnected",
                    Data = new
                    {
                        Message = "重连完成，已同步所有消息",
                        MissedCount = missedEvents.Count()
                    }
                };
            }

            // 3. 发送初始状态
            yield return new SseEvent
            {
                EventType = "init",
                Data = new
                {
                    ServerVersion = "1.0.0",
                    MaxReconnectAttempts = 10,
                    HeartbeatInterval = 30
                }
            };

            var heartbeatCount = 0;

            // 4. 主事件循环
            while (!cancellationToken.IsCancellationRequested)
            {

                // 心跳事件
                if (heartbeatCount % 6 == 0) // 每30秒发送心跳（假设每5秒循环一次）
                {
                    var count = await _eventBuffer.GetConnectedClientsAsync();
                    yield return new SseEvent
                    {
                        EventType = "heartbeat",
                        Data = new
                        {
                            Timestamp = DateTime.UtcNow,
                            ServerStatus = "healthy",
                            ActiveClients = count
                        },
                        Retry = 5000
                    };
                }

                // 模拟随机事件
                if (Random.Shared.Next(0, 10) > 7) // 30% 概率发送事件
                {
                    var randomEvent = new SseEvent
                    {
                        EventType = "update",
                        Data = new
                        {
                            Value = Random.Shared.Next(1, 100),
                            Message = "实时更新",
                            Sequence = heartbeatCount
                        }
                    };

                    // 添加到缓冲区
                    await _eventBuffer.AddEventAsync(randomEvent);
                    // 更新客户端最后事件ID
                    await _eventBuffer.UpdateClientLastEventIdAsync(clientId, randomEvent.Id);

                    yield return randomEvent;
                }

                heartbeatCount++;
                await Task.Delay(5000, cancellationToken); // 5秒间隔
            }

            _logger.LogInformation("客户端 {ClientId} 事件流结束", clientId);
        }
        finally
        {
            _logger.LogDebug("清理客户端 {ClientId} 的资源", clientId);
        }
    }

    /// <summary>
    /// 生成聊天流
    /// </summary>
    private async IAsyncEnumerable<SseEvent> GenerateChatStream(
        string question,
        string? lastEventId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 发送开始事件
        yield return new SseEvent
        {
            EventType = "chat-start",
            Data = new
            {
                Question = question,
                Timestamp = DateTime.UtcNow,
                Processing = true
            },
            Retry = 3000
        };

        var response = $"我正在思考你的问题: '{question}'。这是一个流式响应示例，演示了SSE如何逐字返回数据。";
        var words = response.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < words.Length; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var wordEvent = new SseEvent
            {
                EventType = "chat-chunk",
                Data = new
                {
                    Chunk = words[i] + (i < words.Length - 1 ? " " : ""),
                    Index = i,
                    Total = words.Length,
                    IsComplete = false
                }
            };

            // 添加到缓冲区以便重连
            await _eventBuffer.AddEventAsync(wordEvent);

            yield return wordEvent;
            await Task.Delay(150, cancellationToken);
        }

        // 发送完成事件
        var completionEvent = new SseEvent
        {
            EventType = "chat-complete",
            Data = new
            {
                Message = "回答完成",
                Question = question,
                TotalWords = words.Length,
                Timestamp = DateTime.UtcNow,
                IsComplete = true
            },
            Retry = 5000
        };

        await _eventBuffer.AddEventAsync(completionEvent);
        yield return completionEvent;
    }

    /// <summary>
    /// 生成进度流
    /// </summary>
    private async IAsyncEnumerable<SseEvent> GenerateProgressStream(
        string taskName,
        string? lastEventId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var totalSteps = 20;
        var step = 0;

        // 检查是否从中间进度恢复
        if (!string.IsNullOrEmpty(lastEventId))
        {
            // 尝试从事件中提取进度信息
            var lastEvent = (await _eventBuffer.GetEventsSinceAsync(lastEventId))
                .LastOrDefault(e => e.EventType == "progress");

            if (lastEvent?.Data is JsonElement jsonElement)
            {
                if (jsonElement.TryGetProperty("currentStep", out var currentStep))
                {
                    step = currentStep.GetInt32();
                    _logger.LogInformation("从进度 {Step} 恢复任务", step);
                }
            }
        }

        for (; step <= totalSteps; step++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var progressEvent = new SseEvent
            {
                EventType = "progress",
                Data = new
                {
                    TaskName = taskName,
                    CurrentStep = step,
                    TotalSteps = totalSteps,
                    Percentage = step * 100 / totalSteps,
                    Message = $"正在处理 {taskName}: {step}/{totalSteps}",
                    Timestamp = DateTime.UtcNow
                },
                Retry = 3000
            };

            await _eventBuffer.AddEventAsync(progressEvent);
            yield return progressEvent;

            await Task.Delay(300, cancellationToken);
        }

        // 完成事件
        yield return new SseEvent
        {
            EventType = "task-complete",
            Data = new
            {
                TaskName = taskName,
                Message = "任务已完成",
                TotalTime = totalSteps,
                Timestamp = DateTime.UtcNow
            }
        };
    }

    /// <summary>
    /// 生成实时数据流
    /// </summary>
    private async IAsyncEnumerable<SseEvent> GenerateLiveDataStream(
        string dataType,
        string? lastEventId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var count = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var dataEvent = new SseEvent
            {
                EventType = dataType,
                Data = dataType switch
                {
                    "metrics" => new
                    {
                        CPU = Random.Shared.NextDouble() * 100,
                        Memory = Random.Shared.NextDouble() * 100,
                        Network = Random.Shared.NextDouble() * 1000,
                        Requests = Random.Shared.Next(100, 10000),
                        Timestamp = DateTime.UtcNow
                    },
                    "stocks" => new
                    {
                        Symbol = "AAPL",
                        Price = 150 + (Random.Shared.NextDouble() - 0.5) * 10,
                        Change = (Random.Shared.NextDouble() - 0.5) * 5,
                        Volume = Random.Shared.Next(1000, 1000000),
                        Timestamp = DateTime.UtcNow
                    },
                    _ => new
                    {
                        Value = Random.Shared.Next(1, 100),
                        Count = count,
                        Timestamp = DateTime.UtcNow,
                        DataType = dataType
                    }
                },
                Retry = 2000
            };

            await _eventBuffer.AddEventAsync(dataEvent);
            yield return dataEvent;

            count++;
            await Task.Delay(2000, cancellationToken);
        }
    }
    */

    private class ClientDisposer(
        string clientId,
        ConcurrentDictionary<string, CancellationTokenSource> tokens,
        CancellationTokenSource cts,
        ILogger logger) : IDisposable
    {
        private readonly string _clientId = clientId;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _tokens = tokens;
        private readonly CancellationTokenSource _cts = cts;
        private readonly ILogger _logger = logger;
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _tokens.TryRemove(_clientId, out _);
                _cts.Dispose();
                _logger.LogInformation("客户端断开连接: {ClientId}", _clientId);
                _disposed = true;
            }
        }
    }
}

public class EventCleanupService(
    IEventBufferService eventBuffer,
    ILogger<EventCleanupService> logger) : BackgroundService
{
    private readonly IEventBufferService _eventBuffer = eventBuffer;
    private readonly ILogger<EventCleanupService> _logger = logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("事件清理服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);

                await _eventBuffer.ClearOldEventsAsync(maxAgeInMinutes: 30);
                _logger.LogDebug("执行事件清理完成");
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("事件清理服务已停止");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "事件清理服务执行失败");
            }
        }
    }
}