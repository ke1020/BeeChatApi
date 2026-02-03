using System.Runtime.CompilerServices;
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

namespace Ke.Tasks;

/// <summary>
/// 语音转写任务处理器
/// </summary>
/// <param name="logger"></param>
public class AsrTaskProcessor(IServiceProvider serviceProvider) : ITaskProcessor
{
    /// <summary>
    /// 日志记录器
    /// </summary>
    private readonly ILogger<AsrTaskProcessor> _logger = serviceProvider.GetRequiredService<ILogger<AsrTaskProcessor>>();
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

    public async Task ProcessAsync(TaskInfo task, ChannelWriter<object> channelWriter, CancellationToken cancellationToken)
    {
        var files = task.Files ?? [];
        // 任务开始
        _logger.LogInformation("开始语音转写任务: {TaskName}，共 {FileCount} 个文件", task.TaskName, files.Length);

        if (files.Length == 0)
        {
            _logger.LogWarning("没有需要处理的文件，退出任务");
            return;
        }

        /*
        // 创建进度更新定时器
        using var progressTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(ProgressUpdateIntervalMs));
        var progressUpdateTask = Task.Run(async () =>
        {
            while (await progressTimer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    var progressEvent = CreateTaskProgressEvent(task);
                    await channelWriter.WriteAsync(progressEvent, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "发送进度更新失败");
                }
            }
        }, cancellationToken);
        */

        // 处理每个文件
        for (int fileIndex = task.CurrentFileIndex; fileIndex < files.Length; fileIndex++)
        {
            var filePath = files[fileIndex];
            filePath = @"C:\Users\ke\dev\proj\tools\BeeChat\ChatApi\host\Ke.Chat.HttpApi.Host\temp\[Milan Jovanović] Why I'm Finally Trying Wolverine (and How It Compares) (gvl_6V2Oc6s).mp4";
            var fileName = Path.GetFileName(filePath);
            var fileId = Guid.NewGuid().ToString("N");

            // 开始处理文件
            _logger.LogInformation("开始处理文件: {FileName} (索引: {Index}/{Total})",
                fileName, fileIndex + 1, files.Length)
                ;

            task.CurrentFileIndex = fileIndex;

            // 步骤1: 转码
            await ProcessTranscodingAsync(filePath, async (progress) =>
            {
                //var progressEvent = CreateFileProgressEvent(task, fileProgress, TaskWeightType.Transcode.ToString(), progress);
                await channelWriter.WriteAsync(new
                {
                    FileId = fileId,
                    EventType = "task.transcoding",
                    Progress = progress
                }, cancellationToken);
            }, cancellationToken);

            // 转码完成
            _logger.LogInformation("文件转码完成: {FileName}", filePath);

            // 步骤2: 识别
            await ProcessRecognitionAsync(filePath, async (progress) =>
            {
                //var progressEvent = CreateFileProgressEvent(task, fileProgress, TaskWeightType.ASR.ToString(), progress);
                await channelWriter.WriteAsync(new
                {
                    FileId = fileId,
                    EventType = "task.asr",
                    Progress = progress
                }, cancellationToken);
            }, cancellationToken);

            _logger.LogInformation("文件识别完成: {FileName}", filePath);
        }

        // 任务完成
        task.Status = TaskStatus.Completed;
        task.EndTime = DateTime.UtcNow;
        await channelWriter.WriteAsync(CreateTaskCompleteEvent(task), cancellationToken);
        _logger.LogInformation("任务完成: {TaskName}，总耗时: {Time:F2}s，总体权重进度: {WeightProgress:F2}%",
            task.TaskName, (task.EndTime - task.StartTime)?.TotalSeconds ?? 0,
            100);
    }

    /// <summary>
    /// 带进度报告的文件转码处理
    /// </summary>
    private async Task ProcessTranscodingAsync(
        string filePath,
        Func<double, Task> progressCallback,
        CancellationToken cancellationToken)
    {
        //fileItem.Status = FileStatus.Transcoding;
        //fileItem.FilePath = @"C:\Users\ke\dev\proj\tools\BeeChat\ChatApi\host\Ke.Chat.HttpApi.Host\temp\[Milan Jovanović] Why I'm Finally Trying Wolverine (and How It Compares) (gvl_6V2Oc6s).mp4";
        var output = GetOutputFilePath(filePath);

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
                    _logger.LogWarning(ex, "发送转码进度失败");
                }
            }
        }

        int samplingRate = 16000;
        var analysis = FFProbe.Analyse(filePath);
        var progressor = FFMpegArguments
            .FromFileInput(filePath)
            .OutputToFile(output, true, opts =>
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
        string filePath,
        Func<double, Task> progressCallback,
        CancellationToken cancellationToken)
    {
        //fileItem.Status = FileStatus.Recognizing;
        var output = GetOutputFilePath(filePath);

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
                    _logger.LogWarning(ex, "发送识别进度失败");
                }
            }
        };

        var funAsrNano = _sherpaOptions.FunAsrNano ??
            throw new ArgumentNullException(nameof(FunAsrNanoModel));

        var response = await _asr.RecognizeAsync(new SherpaSpeechRecognizeRequest(output)
        {
            FunAsrNano = new FunAsrNanoModel(funAsrNano.EncoderAdaptor,
                funAsrNano.LLM,
                funAsrNano.Embedding,
                funAsrNano.Tokenizer),
            Tokens = string.Empty,
            Progress = progress
        }, cancellationToken);

        File.WriteAllText(@"C:\Users\ke\dev\proj\tools\BeeChat\ChatApi\host\Ke.Chat.HttpApi.Host\temp\16k.srt", response.Text);

        // 更新已完成权重
        //UpdateFileWeightProgress(fileItem, weight);
    }

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
            /*
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
            */
        };
    }

    private static SseEvent CreateTaskCompleteEvent(TaskInfo task)
    {
        return new SseEvent
        {
            Id = Guid.NewGuid().ToString(),
            EventType = "task-complete"
        };
    }

    private static string GetOutputFilePath(ReadOnlySpan<char> filePath)
    {
        var path = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        return Path.Join(path, string.Concat(fileName, ".wav"));
    }
}