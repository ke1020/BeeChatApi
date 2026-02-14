using System.Runtime.CompilerServices;
using System.Text.Json;
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
        string? lastEventId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(request.Prompt);

        // 创建 Channel 用于收集 SSE 事件
        var channel = Channel.CreateUnbounded<SseEvent>(new UnboundedChannelOptions
        {
            SingleWriter = false, // 允许多个写入者
            SingleReader = true
        });

        // session 管理
        var sessionService = _serviceProvider.GetRequiredService<IChatSessionAppService>();
        // 创建一个新的会话
        var session = new CreateChatSessionDto(Guid.CreateVersion7(), request.Prompt);
        int requestMessageId = (request.ParentId ?? 0) + 1;

        // 连接就绪，分配消息 ID
        await channel.Writer.WriteReadyAsync(request.ParentId, requestMessageId, cancellationToken: cancellationToken);

        // 恢复会话
        await ResumeAsync(request, channel.Writer, lastEventId, cancellationToken);

        // 消息对象
        var chatRequestMessage = new ChatMessageInputDto
        {
            ParentId = request.ParentId,
            MessageId = requestMessageId,
            Role = "USER",
            ThinkingEnabled = request.ThinkingEnabled,
            Files = request.RefFileIds,
            Fragments =
            [
                new ChatMessageFragment{ Type="REQUEST", Content = request.Prompt }
            ]
        };
        // 像会话添加首条消息
        session.Messages.Add(chatRequestMessage);
        // 保存会话
        await sessionService.CreateAsync(session, cancellationToken);

        var chatResponseMessage = new ChatMessageInputDto
        {
            ParentId = chatRequestMessage.MessageId,
            MessageId = chatRequestMessage.MessageId + 1,
            Role = "ASSISTANT",
            Fragments =
            [
                new ChatMessageFragment { Type = "THINK", Task = new TaskInfo
                {
                    InputFiles = request.RefFileIds ?? [],
                    OutputFiles = []
                } },
                new ChatMessageFragment { Type = "RESPONSE" }
            ]
        };
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

                    var taskInfo = chatResponseMessage.Fragments[0].Task!;

                    // 创建任务处理器
                    var taskProcessor = processFactory.Create(taskType);

                    // 任务完成
                    taskProcessor.TaskCompleted += async (s, e) =>
                    {
                        taskInfo.Status = Models.TaskStatus.Completed;
                        taskInfo.EndTime = DateTime.UtcNow;
                        // await channel.Writer.WriteAsync(new TaskCompletedEvent("任务完成"), cancellationToken);
                        _logger.LogInformation("任务完成: {TaskId}，总耗时: {Time:F2}s，总体权重进度: {WeightProgress:F2}%",
                            taskInfo.Id, (taskInfo.EndTime - taskInfo.StartTime)?.TotalSeconds ?? 0,
                            100);

                        taskInfo.OutputFiles = e.Task.SubTasks?
                            .Where(x => x.Status == Models.TaskStatus.Completed)
                            .Select(x => x.OutputFile ?? string.Empty).ToList() ?? []
                            ;

                        // 最终响应信息
                        // chatResponseMessage.Fragments[1].Content = "任务处理完成。处理结果保存在以下目录：";
                        // 增加消息
                        await sessionService.AddMessageAsync(session.SessionId, chatResponseMessage, cancellationToken);
                    };

                    // 处理任务
                    await taskProcessor.ProcessAsync(taskInfo, channel.Writer, cancellationToken);

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "异常消息: {Message}", ex.Message);
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
        string? lastEventId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(lastEventId))
        {
            return;
        }

        using (_eventBuffer)
        {
            var events = await _eventBuffer.GetEventsSinceAsync(lastEventId, cancellationToken);
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