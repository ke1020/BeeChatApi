using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Ke.Tasks.Abstractions;
using Ke.Tasks.Models;
using Ke.Tasks.Models.Chats;
using Microsoft.Extensions.Logging;

namespace Ke.Tasks;

/// <summary>
/// 对话完成
/// </summary>
/// <param name="logger"></param>
/// <param name="eventBuffer"></param>
public class ChatCompletion(ILogger<ChatCompletion> logger, IEventBufferService eventBuffer, IServiceProvider serviceProvider) : IChat
{
    private readonly IEventBufferService _eventBuffer = eventBuffer;
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public async IAsyncEnumerable<object> SendAsync(ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        // 创建 Channel 用于收集 SSE 事件
        var channel = Channel.CreateUnbounded<object>(new UnboundedChannelOptions
        {
            SingleWriter = false, // 允许多个写入者
            SingleReader = true
        });

        int requestMessageId = request.ParentId ?? 1;
        int responseMessageId = requestMessageId + 1;
        // 连接就绪，分配消息 ID
        await channel.Writer.WriteAsync(new
        {
            Event = "ready",
            RequestMessageId = requestMessageId,
            ResponseMessageId = responseMessageId
        }, cancellationToken);

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
                    ITaskProcessor? taskProcessor = request.TaskType switch
                    {
                        "asr" => new AsrTaskProcessor(_serviceProvider),
                        _ => null
                    };

                    if (taskProcessor is not null)
                    {
                        await taskProcessor.ProcessAsync(new TaskInfo
                        {
                            Files = request.RefFileIds!
                        }, channel.Writer, cancellationToken);
                    }
                }
                catch (Exception)
                {
                    //Logger.LogError(ex, "处理任务时发生异常");
                    await channel.Writer.WriteAsync(new {}, cancellationToken);
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
        ChannelWriter<object> channelWriter,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.ClientStreamId))
        {
            return;
        }

        using (_eventBuffer)
        {
            var events = await _eventBuffer.GetEventsSinceAsync(request.ClientStreamId, cancellationToken);
            var lastEvent = events.LastOrDefault(e => e.EventType == "file-progress" || e.EventType == "task-progress");
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="request"></param>
    /// <param name="channelWriter"></param>
    /// <param name="responseMessageId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task FirstMessageAsync(ChatRequest request,
        ChannelWriter<object> channelWriter,
        int responseMessageId,
        CancellationToken cancellationToken)
    {
        // 创建会话
        await channelWriter.WriteAsync(new
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
        }, cancellationToken);
    }
}