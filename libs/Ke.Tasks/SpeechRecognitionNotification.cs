using System.Runtime.CompilerServices;
using System.Text.Json;
using Ke.Tasks.Abstractions;
using Ke.Tasks.Models;
using Ke.Tasks.SSE.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskStatus = Ke.Tasks.Models.TaskStatus;

namespace Ke.Tasks;

/// <summary>
/// 任务进度服务
/// </summary>
/// <param name="eventBuffer"></param>
/// <param name="logger"></param>
public class SpeechRecognitionNotification(ILogger<SpeechRecognitionNotification> logger,
    IOptions<TaskWeightOptions> taskWeightOptions,
    IEventBufferService eventBuffer) :
    TaskNotificationBase<SpeechRecognitionNotificationRequest>(logger, taskWeightOptions.Value),
    ISpeechRecognitionNotification
{
    private readonly IEventBufferService _eventBuffer = eventBuffer;

    /// <summary>
    /// 生成多文件处理进度流
    /// </summary>
    public override async IAsyncEnumerable<SseEvent> Send(SpeechRecognitionNotificationRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        // 验证权重配置
        ValidateWeights(TaskWeightOptions.SpeechRecognize);

        // 获取转码权重
        var transcodeWeight = TaskWeightOptions.SpeechRecognize
            .FirstOrDefault(w => w.WeightType == TaskWeightType.Transcode)?.Weight ?? 0
            ;
        // 获取 ASR 权重
        var asrWeight = TaskWeightOptions.SpeechRecognize
            .FirstOrDefault(w => w.WeightType == TaskWeightType.ASR)?.Weight ?? 0
            ;

        // 创建任务实例
        var task = new TaskInfo
        {
            TaskName = request.TaskName,
            Files = [.. request.FilePaths.Select((path, index) =>
                CreateTaskItem(path, index, transcodeWeight, asrWeight)
                )],
            Status = TaskStatus.Processing
        };

        // 尝试从断点恢复
        if (!string.IsNullOrEmpty(request.LastEventId))
        {
            using (_eventBuffer)
            {
                var lastEvent = _eventBuffer.GetEventsSince(request.LastEventId)
                    .LastOrDefault(e => e.EventType == "file-progress" || e.EventType == "task-progress")
                    ;

                if (lastEvent?.Data is JsonElement jsonElement)
                {
                    Logger.LogInformation("从事件 {EventId} 恢复任务", request.LastEventId);

                    // 尝试恢复任务状态
                    if (jsonElement.TryGetProperty("taskId", out var taskIdProp) &&
                        taskIdProp.GetString() == task.TaskId)
                    {
                        if (jsonElement.TryGetProperty("currentFileIndex", out var fileIndexProp))
                        {
                            task.CurrentFileIndex = fileIndexProp.GetInt32();
                        }

                        // 恢复权重进度
                        if (jsonElement.TryGetProperty("completedSubtaskWeight", out var weightProp) &&
                            task.CurrentFileIndex < task.Files.Count)
                        {
                            var currentFile = task.Files[task.CurrentFileIndex];
                            currentFile.Metadata["completedSubtaskWeight"] = weightProp.GetInt32();
                        }

                        Logger.LogInformation("从断点恢复任务 {TaskId}，文件索引 {FileIndex}",
                            task.TaskId, task.CurrentFileIndex)
                            ;
                    }
                }
            }
        }

        // 发送任务开始事件
        yield return CreateTaskStartEvent(task);
        Logger.LogInformation("开始处理任务: {TaskName}，共 {FileCount} 个文件", task.TaskName, task.Files.Count);

        // 处理每个文件
        for (int fileIndex = task.CurrentFileIndex; fileIndex < task.Files.Count; fileIndex++)
        {
            var fileItem = task.Files[fileIndex];
            fileItem.Status = FileStatus.Preparing;
            fileItem.StartTime = DateTime.UtcNow;
            task.CurrentFileIndex = fileIndex;

            // 发送文件开始事件
            yield return CreateFileStartEvent(task, fileItem);
            Logger.LogInformation("开始处理文件: {FileName} (索引: {Index}/{Total})",
                fileItem.FileName, fileIndex + 1, task.Files.Count)
                ;

            // 步骤1: 转码（使用分配的权重）
            await ProcessTranscoding(fileItem, transcodeWeight, cancellationToken);

            // 发送转码完成事件
            yield return CreateSubtaskCompleteEvent(task, fileItem, TaskWeightType.Transcode,
                transcodeWeight, GetFileCompletedWeight(fileItem))
                ;
            Logger.LogInformation("文件转码完成: {FileName}，累计权重: {Weight}/100",
                    fileItem.FileName, GetFileCompletedWeight(fileItem))
                    ;

            // 步骤2: 识别（使用分配的权重）
            await ProcessRecognition(fileItem, asrWeight, cancellationToken);

            // 发送识别完成事件
            yield return CreateSubtaskCompleteEvent(task, fileItem, TaskWeightType.ASR,
                asrWeight, GetFileCompletedWeight(fileItem))
                ;
            Logger.LogInformation("文件识别完成: {FileName}，累计权重: {Weight}/100",
                fileItem.FileName, GetFileCompletedWeight(fileItem))
                ;

            // 文件完成
            fileItem.Status = FileStatus.Completed;
            fileItem.EndTime = DateTime.UtcNow;

            // 确保权重累计到100
            fileItem.Metadata["completedSubtaskWeight"] = 100;

            // 发送文件完成事件
            yield return CreateFileCompleteEvent(task, fileItem, GetFileCompletedWeight(fileItem));
            Logger.LogInformation("文件处理完成: {FileName}，总权重: 100/100，耗时: {Time:F2}s",
                fileItem.FileName, (fileItem.EndTime - fileItem.StartTime)?.TotalSeconds ?? 0)
                ;

            // 发送任务总体进度更新
            yield return CreateTaskProgressEvent(task);
            Logger.LogInformation("任务进度更新: {TaskName}，总体进度: {OverallProgress:F2}%，文件进度: {FileProgress:F2}%，已完成 {Completed}/{Total} 个文件",
                task.TaskName, task.OverallProgress, GetTaskWeightProgress(task),
                task.Files.Count(f => f.Status == FileStatus.Completed), task.Files.Count);
        }

        // 任务完成
        task.Status = TaskStatus.Completed;
        task.EndTime = DateTime.UtcNow;
        yield return CreateTaskCompleteEvent(task);
        Logger.LogInformation("任务完成: {TaskName}，总耗时: {Time:F2}s，总体权重进度: {WeightProgress:F2}%",
            task.TaskName, (task.EndTime - task.StartTime)?.TotalSeconds ?? 0,
            GetTaskWeightProgress(task))
            ;
    }

    private static SseEvent CreateTaskStartEvent(TaskInfo task)
    {
        return new SseEvent
        {
            Id = Guid.NewGuid().ToString(),
            EventType = "task-start",
            Data = new
            {
                taskId = task.TaskId,
                taskName = task.TaskName,
                totalFiles = task.Files.Count,
                startTime = task.StartTime,
                message = $"开始处理任务: {task.TaskName}，共 {task.Files.Count} 个文件"
            }
        };
    }

    private static SseEvent CreateFileStartEvent(TaskInfo task, TaskItem fileItem)
    {
        return new SseEvent
        {
            Id = Guid.NewGuid().ToString(),
            EventType = "file-start",
            Data = new
            {
                taskId = task.TaskId,
                fileId = fileItem.FileId,
                fileName = fileItem.FileName,
                fileIndex = task.Files.IndexOf(fileItem) + 1,
                totalFiles = task.Files.Count,
                message = $"开始处理文件: {fileItem.FileName}"
            }
        };
    }

    private static SseEvent CreateTaskProgressEvent(TaskInfo task)
    {
        return new SseEvent
        {
            Id = Guid.NewGuid().ToString(),
            EventType = "task-progress",
            Data = new
            {
                taskId = task.TaskId,
                taskName = task.TaskName,
                overallProgress = task.OverallProgress,
                completedFiles = task.Files.Count(f => f.Status == FileStatus.Completed),
                totalFiles = task.Files.Count,
                completedSteps = task.CompletedSteps,
                totalSteps = task.TotalSteps,
                currentFileIndex = task.CurrentFileIndex,
                currentStepInFile = task.CurrentStepInFile,
                message = $"总体进度: {task.OverallProgress:F1}%，已完成 {task.Files.Count(f => f.Status == FileStatus.Completed)}/{task.Files.Count} 个文件",
                timestamp = DateTime.UtcNow
            },
            Retry = 3000
        };
    }

    private static SseEvent CreateTaskCompleteEvent(TaskInfo task)
    {
        return new SseEvent
        {
            Id = Guid.NewGuid().ToString(),
            EventType = "task-complete",
            Data = new
            {
                taskId = task.TaskId,
                taskName = task.TaskName,
                status = "completed",
                totalTime = (task.EndTime - task.StartTime)?.TotalSeconds,
                totalFiles = task.Files.Count,
                successfulFiles = task.Files.Count(f => f.Status == FileStatus.Completed),
                failedFiles = task.Files.Count(f => f.Status == FileStatus.Failed),
                message = $"任务完成，成功 {task.Files.Count(f => f.Status == FileStatus.Completed)} 个文件，失败 {task.Files.Count(f => f.Status == FileStatus.Failed)} 个文件"
            }
        };
    }

    private static SseEvent CreateFileErrorEvent(TaskInfo task, TaskItem fileItem, string error)
    {
        return new SseEvent
        {
            Id = Guid.NewGuid().ToString(),
            EventType = "file-error",
            Data = new
            {
                taskId = task.TaskId,
                fileId = fileItem.FileId,
                fileName = fileItem.FileName,
                error = error,
                message = $"文件处理失败: {fileItem.FileName}"
            }
        };
    }

    private static SseEvent CreateTaskErrorEvent(TaskInfo task, string error)
    {
        return new SseEvent
        {
            Id = Guid.NewGuid().ToString(),
            EventType = "task-error",
            Data = new
            {
                taskId = task.TaskId,
                taskName = task.TaskName,
                error = error,
                message = $"任务处理失败: {task.TaskName}"
            }
        };
    }

    private static SseEvent CreateTaskCancelledEvent(TaskInfo task)
    {
        return new SseEvent
        {
            Id = Guid.NewGuid().ToString(),
            EventType = "task-cancelled",
            Data = new
            {
                taskId = task.TaskId,
                taskName = task.TaskName,
                message = "任务已取消",
                completedFiles = task.Files.Count(f => f.Status == FileStatus.Completed),
                totalFiles = task.Files.Count
            }
        };
    }

    /// <summary>
    /// 处理转码
    /// </summary>
    private static async Task ProcessTranscoding(TaskItem fileItem, int weight, CancellationToken cancellationToken)
    {
        fileItem.Status = FileStatus.Transcoding;

        // 模拟转码处理时间（实际应调用转码服务）
        await Task.Delay(1000, cancellationToken);

        // 更新转码进度为100%
        UpdateSubtaskProgress(fileItem, "transcoding", 100);

        // 更新已完成权重
        UpdateFileWeightProgress(fileItem, weight);

        fileItem.CompletedSteps++;
    }

    /// <summary>
    /// 处理识别
    /// </summary>
    private static async Task ProcessRecognition(TaskItem fileItem, int weight, CancellationToken cancellationToken)
    {
        fileItem.Status = FileStatus.Recognizing;

        // 模拟识别处理时间（实际应调用识别服务）
        await Task.Delay(1500, cancellationToken);

        // 更新识别进度为100%
        UpdateSubtaskProgress(fileItem, "recognition", 100);

        // 更新已完成权重
        UpdateFileWeightProgress(fileItem, weight);

        fileItem.CompletedSteps++;
    }

    /// <summary>
    /// 更新子任务进度
    /// </summary>
    private static void UpdateSubtaskProgress(TaskItem fileItem, string subtaskName, int progress)
    {
        if (fileItem.Metadata["subtaskProgress"] is Dictionary<string, int> subtaskProgress)
        {
            subtaskProgress[subtaskName] = progress;
        }
    }

    /// <summary>
    /// 更新文件权重进度
    /// </summary>
    private static void UpdateFileWeightProgress(TaskItem fileItem, int weight)
    {
        var currentWeight = GetFileCompletedWeight(fileItem);
        fileItem.Metadata["completedSubtaskWeight"] = currentWeight + weight;
    }

    /// <summary>
    /// 获取文件已完成的权重
    /// </summary>
    private static int GetFileCompletedWeight(TaskItem fileItem)
    {
        return fileItem.Metadata.TryGetValue("completedSubtaskWeight", out var weight)
            ? (int)weight
            : 0;
    }

    /// <summary>
    /// 获取任务的权重进度
    /// </summary>
    private static double GetTaskWeightProgress(TaskInfo task)
    {
        if (task.Files.Count == 0) return 0;

        var totalWeightForAllFiles = task.Files.Count * 100; // 每个文件100权重
        var completedWeightForAllFiles = task.Files.Sum(GetFileCompletedWeight);

        return (double)completedWeightForAllFiles / totalWeightForAllFiles * 100;
    }

    private static SseEvent CreateSubtaskCompleteEvent(TaskInfo task, TaskItem fileItem,
        TaskWeightType taskWeightType, int subtaskWeight, int completedWeight)
    {
        var subtaskName = taskWeightType.ToString();
        var subtaskProgress = fileItem.Metadata["subtaskProgress"] as Dictionary<string, int>;
        var subtaskPercentage = subtaskProgress?[subtaskName] ?? 0;

        return new SseEvent
        {
            Id = Guid.NewGuid().ToString(),
            EventType = "subtask-complete",
            Data = new
            {
                taskId = task.TaskId,
                fileId = fileItem.FileId,
                fileName = fileItem.FileName,
                subtask = subtaskName,
                subtaskWeight,
                subtaskProgress = subtaskPercentage,
                fileWeightProgress = completedWeight,
                totalWeight = 100,
                message = $"{taskWeightType.ToFriendlyString()} 完成 (权重: {subtaskWeight}%，文件累计权重: {completedWeight}/100)"
            }
        };
    }

    /// <summary>
    /// 创建任务项
    /// </summary>
    private static TaskItem CreateTaskItem(string filePath, int index, int transcodeWeight, int asrWeight)
    {
        return new TaskItem
        {
            FileName = Path.GetFileName(filePath),
            FilePath = filePath,
            TotalSteps = 2,
            Metadata = new Dictionary<string, object>
            {
                ["index"] = index,
                ["size"] = new FileInfo(filePath).Length,
                ["extension"] = Path.GetExtension(filePath),
                ["transcodingWeight"] = transcodeWeight,
                ["recognitionWeight"] = asrWeight,
                ["completedSubtaskWeight"] = 0, // 已完成的权重
                ["subtaskProgress"] = new Dictionary<string, int>
                {
                    [nameof(TaskWeightType.Transcode)] = 0,
                    [nameof(TaskWeightType.ASR)] = 0
                }
            }
        };
    }

    private static SseEvent CreateFileCompleteEvent(TaskInfo task, TaskItem fileItem, int completedWeight)
    {
        var subtaskProgress = fileItem.Metadata["subtaskProgress"] as Dictionary<string, int>;

        return new SseEvent
        {
            Id = Guid.NewGuid().ToString(),
            EventType = "file-complete",
            Data = new
            {
                taskId = task.TaskId,
                fileId = fileItem.FileId,
                fileName = fileItem.FileName,
                status = "completed",
                fileWeightProgress = completedWeight,
                totalWeight = 100,
                fileProgress = (double)completedWeight / 100 * 100,
                subtaskProgress,
                processingTime = (fileItem.EndTime - fileItem.StartTime)?.TotalSeconds,
                message = $"文件处理完成: {fileItem.FileName} (权重: {completedWeight}/100)"
            }
        };
    }
}