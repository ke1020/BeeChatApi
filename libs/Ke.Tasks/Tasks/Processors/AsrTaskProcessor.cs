using System.Threading.Channels;
using FFMpegCore;
using Ke.Ai.Sherpa.Abstractions;
using Ke.Ai.Sherpa.Speeches.Models;
using Ke.Tasks.Abstractions;
using Ke.Tasks.Models;
using Ke.Tasks.SSE.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskStatus = Ke.Tasks.Models.TaskStatus;

namespace Ke.Tasks.Processors;

/// <summary>
/// 语音转写任务处理器
/// </summary>
/// <param name="logger"></param>
public class AsrTaskProcessor(IServiceProvider serviceProvider)
    : TaskProcessorBase<AsrTaskProcessor>(serviceProvider)
{
    /// <summary>
    /// 语音识别服务配置
    /// </summary>
    private readonly SherpaOptions _sherpaOptions = serviceProvider.GetRequiredService<IOptions<SherpaOptions>>().Value
        ?? throw new ArgumentNullException(nameof(_sherpaOptions));
    /// <summary>
    /// 语音识别任务处理器
    /// </summary>
    private readonly ISherpaSpeechRecognizer _asr = serviceProvider.GetRequiredService<ISherpaSpeechRecognizer>();
    /// <summary>
    /// 进度更新频率（毫秒）
    /// </summary>
    private const int ProgressUpdateIntervalMs = 2000;

    public override async Task ProcessAsync(TaskInfo task, ChannelWriter<SseEvent> channelWriter,
        CancellationToken cancellationToken)
    {
        // 任务开始
        Logger.LogInformation("开始语音转写任务: 任务标识：{TaskId}，共 {FileCount} 个文件",
            task.Id,
            string.Join(',', task.InputFiles.Count))
            ;

        if (task.InputFiles.Count == 0)
        {
            Logger.LogWarning("没有需要处理的文件，退出任务");
            return;
        }

        // 处理每个文件
        for (int i = 0; i < task.InputFiles.Count; i++)
        {
            //var filePath = files[i];
            var tempPath = @"C:\Users\ke\dev\proj\tools\BeeChat\ChatApi\host\Ke.Chat.HttpApi.Host\temp";
            var filePath = Directory.GetFiles(tempPath).FirstOrDefault();
            var fileName = Path.GetFileName(filePath);
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath)!;
            var tempWav = Path.Combine(tempPath, $"{fileNameWithoutExt}.wav");
            var tempSrt = Path.Combine(tempPath, $"{fileNameWithoutExt}.srt");
            var taskItem = new TaskItem
            {
                InputFile = filePath
            };

            // 开始处理文件
            Logger.LogInformation("开始处理文件: {FileName} (索引: {Index}/{Total})",
                fileName, i + 1, task.InputFiles.Count)
                ;

            try
            {

                // 步骤1: 转码
                await ProcessTranscodingAsync(filePath!, tempWav, async (progress) =>
                {
                    taskItem.Status = TaskStatus.Processing;

                    //var progressEvent = CreateFileProgressEvent(task, fileProgress, TaskWeightType.Transcode.ToString(), progress);
                    await channelWriter.WriteAsync(new TaskProgressEvent(progress)
                    {
                        FileIndex = i
                    }, cancellationToken);
                }, cancellationToken);

                // 转码完成
                Logger.LogInformation("文件转码完成: {FileName}", filePath);

                // 步骤2: 识别
                await ProcessRecognitionAsync(tempWav, tempSrt, async (progress) =>
                {
                    //var progressEvent = CreateFileProgressEvent(task, fileProgress, TaskWeightType.ASR.ToString(), progress);
                    await channelWriter.WriteAsync(new TaskProgressEvent(progress)
                    {
                        FileIndex = i
                    }, cancellationToken);
                }, cancellationToken);

                Logger.LogInformation("文件识别完成: {FileName}", filePath);

                // 任务完成
                taskItem.OutputFile = tempSrt;
                taskItem.Status = TaskStatus.Completed;

                // OnTaskItemCompleted(taskItem);
            }
            catch (OperationCanceledException)
            {
                taskItem.Status = TaskStatus.Cancelled;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message, ex);
                taskItem.Status = TaskStatus.Failed;
            }

            taskItem.EndTime = DateTime.UtcNow;

            // 将结果添加到子任务列表中
            task.SubTasks.Add(taskItem);
        }

        OnTaskCompleted(task);
    }

    /// <summary>
    /// 带进度报告的文件转码处理
    /// </summary>
    private async Task ProcessTranscodingAsync(
        string inputFile,
        string outputWavFile,
        Func<double, Task> progressCallback,
        CancellationToken cancellationToken)
    {
        DateTime lastProgressTime = DateTime.MinValue;
        double lastProgressValue = 0;

        async void OnPercentageProgress(double percentage)
        {
            // 更新转码进度
            // UpdateSubtaskProgress(filePath, TaskWeightType.Transcode, percentage);

            // 检查是否需要发送进度更新（至少间隔500ms且进度变化超过1%）
            var now = DateTime.UtcNow;
            if (lastProgressTime == DateTime.MinValue ||
                (now - lastProgressTime).TotalMilliseconds >= 500 ||
                Math.Abs(percentage - lastProgressValue) >= 1)
            {
                try
                {
                    await progressCallback(percentage);
                    lastProgressTime = now;
                    lastProgressValue = percentage;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "发送转码进度失败");
                }
            }
        }

        int samplingRate = 16000;
        var analysis = FFProbe.Analyse(inputFile);
        var progressor = FFMpegArguments
            .FromFileInput(inputFile)
            .OutputToFile(outputWavFile, true, opts =>
            {
                opts.WithDuration(analysis.Duration);
                opts.WithCustomArgument($"-ac 1 -ar {samplingRate} -acodec pcm_s16le");
            });

        progressor.NotifyOnProgress(OnPercentageProgress, analysis.Duration);

        await progressor
            .CancellableThrough(cancellationToken)
            .ProcessAsynchronously()
            .ConfigureAwait(false);

        // 更新已完成权重
        // UpdateFileWeightProgress(fileItem, weight);
    }

    /// <summary>
    /// 带进度报告的文件识别处理
    /// </summary>
    private async Task ProcessRecognitionAsync(
        string audioFile,
        string outputSrtFile,
        Func<double, Task> progressCallback,
        CancellationToken cancellationToken)
    {
        //fileItem.Status = FileStatus.Recognizing;
        DateTime lastProgressTime = DateTime.MinValue;
        double lastProgressValue = 0;

        var progress = new Progress<Ke.Ai.Models.Progress>();
        progress.ProgressChanged += async (sender, progressReport) =>
        {
            // UpdateSubtaskProgress(filePath, TaskWeightType.ASR, progressReport.Percentage);

            // 检查是否需要发送进度更新
            var now = DateTime.UtcNow;
            if (lastProgressTime == DateTime.MinValue ||
                (now - lastProgressTime).TotalMilliseconds >= 500 ||
                Math.Abs(progressReport.Percentage - lastProgressValue) >= 1)
            {
                try
                {
                    await progressCallback(progressReport.Percentage);
                    lastProgressTime = now;
                    lastProgressValue = progressReport.Percentage;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "发送识别进度失败");
                }
            }
        };

        var funAsrNano = _sherpaOptions.FunAsrNano ??
            throw new ArgumentNullException(nameof(FunAsrNanoModel));

        var response = await _asr.RecognizeAsync(new SherpaSpeechRecognizeRequest(audioFile)
        {
            FunAsrNano = new FunAsrNanoModel(funAsrNano.EncoderAdaptor,
                funAsrNano.LLM,
                funAsrNano.Embedding,
                funAsrNano.Tokenizer),
            Tokens = string.Empty,
            Progress = progress
        }, cancellationToken);

        await File.WriteAllTextAsync(outputSrtFile, response.Text, cancellationToken);
    }

    /*
    /// <summary>
    /// 创建任务进度事件
    /// </summary>
    /// <param name="task"></param>
    /// <param name="progressTimer"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async IAsyncEnumerable<SseEvent> CreateTaskProgressEvent(TaskInfo task,
        PeriodicTimer progressTimer,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await progressTimer.WaitForNextTickAsync(cancellationToken))
        {
            // 定时发送任务进度事件
            yield return CreateTaskProgressEvent(task);
        }
    }

    /// <summary>
    /// 创建任务进度事件
    /// </summary>
    /// <param name="task"></param>
    /// <returns></returns>
    private static SseEvent CreateTaskProgressEvent(TaskInfo task)
    {
        return new SseEvent
        {
            Id = Guid.NewGuid().ToString(),
            EventType = "task-progress",
            Files = [.. task.Files.Select(fileName => new FileTask
                {
                    FileId = Guid.NewGuid().ToString(),
                    FileName = Path.GetFileName(fileName),
                    Percentage = 0
                })]
            
            new
            {
                FileId = Guid.NewGuid().ToString(),
                taskId = task.TaskId,
                taskName = task.TaskName,
                //overallProgress = task.OverallProgress,
                completedFiles = task.Files.Count(f => f.Status == FileStatus.Completed),
                totalFiles = task.Files.Count,
                //completedSteps = task.CompletedSteps,
                //totalSteps = task.TotalSteps,
                currentFileIndex = task.CurrentFileIndex,
                //currentStepInFile = task.CurrentStepInFile,
                message = $"总体进度: {task.Percentage:F1}%，已完成 {task.Files.Count(f => f.Status == FileStatus.Completed)}/{task.Files.Count} 个文件",
                timestamp = DateTime.UtcNow
            },
            Retry = 3000
            
        };
    }
    */
}