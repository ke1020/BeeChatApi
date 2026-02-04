using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Ke.Tasks.Abstractions;
using Ke.Tasks.Models;
using Ke.Tasks.Models.Chats;
using Ke.Tasks.SSE.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ke.Tasks;

/// <summary>
/// 对话完成
/// </summary>
/// <param name="logger"></param>
/// <param name="eventBuffer"></param>
public class ChatCompletion(ILogger<ChatCompletion> logger,
    IEventBufferService eventBuffer,
    IServiceProvider serviceProvider) : IChat
{
    private readonly ILogger<ChatCompletion> _logger = logger;
    private readonly IEventBufferService _eventBuffer = eventBuffer;
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public async IAsyncEnumerable<SseEvent> SendAsync(ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        // 创建 Channel 用于收集 SSE 事件
        var channel = Channel.CreateUnbounded<SseEvent>(new UnboundedChannelOptions
        {
            SingleWriter = false, // 允许多个写入者
            SingleReader = true
        });

        int requestMessageId = request.ParentId ?? 1;

        // 消息对象
        var chatMessage = new ChatMessage
        {
            Id = Guid.CreateVersion7(),
            ParentId = requestMessageId,
            MessageId = requestMessageId + 1,
            Role = requestMessageId % 2 == 0 ? "ASSISTANT" : "USER",
            ThinkingEnabled = request.ThinkingEnabled,
            Fragments =
            [
                new { Type="REQUEST", Content = request.Prompt }
            ]
        };

        // 连接就绪，分配消息 ID
        await channel.Writer.WriteReadyAsync(chatMessage.ParentId, chatMessage.MessageId, cancellationToken: cancellationToken);

        // 恢复会话
        await ResumeAsync(request, channel.Writer, cancellationToken);

        // await FirstMessageAsync(request, channel.Writer, responseMessageId, cancellationToken);

        if (request.TaskType is not null)
        {
            // 启动后台任务处理文件
            _ = Task.Run(async () =>
            {
                try
                {
                    // 获取任务类型 (asr, transcode, tts etc.)
                    if (!Enum.TryParse<TaskType>(request.TaskType, true, out var taskType))
                    {
                        return;
                    }

                    // 获取任务处理器工厂实例
                    var processFactory = _serviceProvider.GetService<ITaskProcessFactory>();
                    if (processFactory is null)
                    {
                        return;
                    }

                    // 创建任务处理器
                    var taskProcessor = processFactory.Create(taskType);

                    // 处理任务
                    await taskProcessor.ProcessAsync(new TaskInfo
                    {
                        Files = request.RefFileIds!
                    }, channel.Writer, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.Message);
                    await channel.Writer.WriteAsync(new TaskErrorEvent("任务异常"), cancellationToken);
                }
                finally
                {
                    channel.Writer.Complete();
                }
            }, cancellationToken);
        }

        // 返回 SSE 事件流
        await foreach (var sseEvent in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return sseEvent;
        }
    }

    /// <summary>
    /// 恢复聊天会话
    /// </summary>
    /// <param name="request"></param>
    /// <param name="channelWriter"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task ResumeAsync(ChatRequest request,
        ChannelWriter<SseEvent> channelWriter,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.ClientStreamId))
        {
            return;
        }

        using (_eventBuffer)
        {
            var events = await _eventBuffer.GetEventsSinceAsync(request.ClientStreamId, cancellationToken);
            var lastEvent = events.LastOrDefault();
            //if(lastEvent.Equals)
        }
    }

    /*
    /// <summary>
    /// 
    /// </summary>
    /// <param name="request"></param>
    /// <param name="channelWriter"></param>
    /// <param name="responseMessageId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task FirstMessageAsync(ChatRequest request,
        ChannelWriter<SseEvent> channelWriter,
        int responseMessageId,
        CancellationToken cancellationToken)
    {
        // 创建会话
        await channelWriter.WriteAsync(new DataEvent<object>(new
        {
            v = new
            {
                response = new
                {
                    messageId = responseMessageId,
                    parentId = request.ParentId,
                    model = string.Empty,
                    role = "ASSISTANT",
                    thinkingEnabled = request.ThinkingEnabled,
                    banEdit = false,
                    banRegenerate = false,
                    status = "WIP",
                    accumulatedTokenUsage = 0,
                    files = request.RefFileIds ?? [],
                    //feedback = null,
                    createAt = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                    searchEnabled = false,
                    //fragments = [],
                    hasPendingFragment = true,
                    autoContinue = false
                }
            }
        }), cancellationToken);
    }
    */
}