using System.Threading.Channels;
using FFMpegCore;
using Ke.Tasks.Abstractions;
using Ke.Tasks.Models;
using Ke.Tasks.SSE.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ke.Tasks.Processors;

public class VideoConcatProcessor(IServiceProvider serviceProvider) : ITaskProcessor
{
    /// <summary>
    /// 日志记录器
    /// </summary>
    private readonly ILogger<VideoConcatProcessor> _logger = serviceProvider.GetRequiredService<ILogger<VideoConcatProcessor>>();
    public event EventHandler<TaskCompletedEventArgs>? TaskCompleted;
    public event EventHandler<TaskItemCompletedEventArgs>? TaskItemCompleted;

    public async Task ProcessAsync(TaskInfo task, ChannelWriter<SseEvent> channelWriter, CancellationToken cancellationToken)
    {
        var files = task.InputFiles ?? [];
        // 任务开始
        _logger.LogInformation("开始进行视频合并任务: {TaskName}，共 {FileCount} 个文件", task.TaskName, files.Length);

        task.OutputFiles = [$"{task.TaskName}.mp4"];

        await FFMpegArguments
            .FromConcatInput(files)
            .OutputToFile(task.OutputFiles[0])
            .ProcessAsynchronously()
            .ConfigureAwait(false)
            ;

        await channelWriter.WriteAsync(new TaskCompletedEvent("任务完成"), cancellationToken);
        TaskCompleted?.Invoke(this, new TaskCompletedEventArgs(task, Models.TaskStatus.Completed));
    }
}