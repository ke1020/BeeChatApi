using System.Threading.Channels;
using FFMpegCore;
using Ke.Tasks.Abstractions;
using Ke.Tasks.Models;
using Ke.Tasks.SSE.Models;
using Microsoft.Extensions.Logging;

namespace Ke.Tasks.Processors;

/// <summary>
/// 视频合并任务处理器
/// </summary>
/// <param name="serviceProvider"></param>
public class VideoMergeProcessor(IServiceProvider serviceProvider) 
    : TaskProcessorBase<VideoMergeProcessor>(serviceProvider)
{
    public override async Task ProcessAsync(TaskInfo task, ChannelWriter<SseEvent> channelWriter, CancellationToken cancellationToken)
    {
        // 任务开始
        Logger.LogInformation("开始进行视频合并任务，任务标识: {TaskId}，文件： {Files}", 
            task.Id, 
            string.Join(',', task.InputFiles))
            ;

        //task.OutputFiles = [$"{task.TaskName}.mp4"];
        string outputFile = $".mp4";
        try
        {
            // 1. 为每个输入文件创建预处理子任务
            var preprocessTasks = new List<TaskItem>();
            foreach (var inputFile in task.InputFiles)
            {
                var item = new TaskItem
                {
                    Id = Guid.CreateVersion7(),
                    InputFile = inputFile
                };
                task.SubTasks.Add(item);
                preprocessTasks.Add(item);
            }

            // 2. 创建合并子任务（不绑定具体输入文件，可以放在最后）
            var mergeItem = new TaskItem
            {
                Id = Guid.CreateVersion7()
            };
            task.SubTasks.Add(mergeItem);

            // 逐个处理预处理子任务
            var tempFiles = new List<string>();
            foreach (var item in preprocessTasks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                item.Status = Models.TaskStatus.Processing;
                item.StartTime = DateTime.UtcNow;

                // 模拟预处理：转码、检查等，生成临时文件
                string tempFile = string.Empty;
                await PreprocessAsync(item.InputFile, tempFile, cancellationToken);
                tempFiles.Add(tempFile);

                // item.OutputFile = tempFile;
                item.Status = Models.TaskStatus.Completed;
                item.EndTime = DateTime.UtcNow;

                // 触发子任务完成事件，用于外部收集（如你的代码中的 tasks 列表）
                OnTaskItemCompleted(item);

                // 推送 SSE 进度事件
                await channelWriter.WriteAsync(new SseEvent($"文件 {item.InputFile} 预处理完成"), cancellationToken);
            }

            // 执行合并子任务
            mergeItem.Status = Models.TaskStatus.Processing;
            mergeItem.StartTime = DateTime.UtcNow;

            await MergeVideosAsync(tempFiles, outputFile, cancellationToken);
            mergeItem.OutputFile = outputFile;
            mergeItem.Status = Models.TaskStatus.Completed;
            mergeItem.EndTime = DateTime.UtcNow;
            OnTaskItemCompleted(mergeItem);
            await channelWriter.WriteAsync(new SseEvent("视频合并完成"), cancellationToken);

            // 清理临时文件（可选，可以作为另一个子任务）
            // ...

            // 更新主任务信息
            task.OutputFiles = [outputFile];
            task.Status = Models.TaskStatus.Completed;
            task.EndTime = DateTime.UtcNow;

            // 触发主任务完成事件
            OnTaskCompleted(task);
        }
        catch (Exception ex)
        {
            task.Status = Models.TaskStatus.Failed;
            task.ErrorMessage = ex.Message;
            task.EndTime = DateTime.UtcNow;
            await channelWriter.WriteAsync(new TaskErrorEvent($"合并失败: {ex.Message}"), cancellationToken);
        }
    }

    private static async Task PreprocessAsync(string? inputFile, 
        string outputFile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(inputFile);

        await FFMpegArguments
            .FromFileInput(inputFile)
            .OutputToFile(outputFile)
            .CancellableThrough(cancellationToken)
            .ProcessAsynchronously()
            .ConfigureAwait(false)
            ;
    }

    /// <summary>
    /// 合并视频文件
    /// </summary>
    /// <param name="inputVideoFiles"></param>
    /// <param name="outputFile"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private static async Task MergeVideosAsync(List<string> inputVideoFiles, 
        string outputFile, 
        CancellationToken cancellationToken = default)
    {
        await FFMpegArguments
            .FromConcatInput(inputVideoFiles)
            .OutputToFile(outputFile)
            .CancellableThrough(cancellationToken)
            .ProcessAsynchronously()
            .ConfigureAwait(false)
            ;
    }
}