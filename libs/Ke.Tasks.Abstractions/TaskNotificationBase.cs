using System.Text;
using Ke.Tasks.Models;
using Ke.Tasks.SSE.Models;
using Microsoft.Extensions.Logging;

namespace Ke.Tasks.Abstractions;

public abstract class TaskNotificationBase<T>(ILogger logger,
    TaskWeightOptions taskWeightOptions)
    : ITaskNotification<T>
    where T : TaskNotificationRequest
{
    /// <summary>
    /// 日志记录器
    /// </summary>
    protected readonly ILogger Logger = logger;
    /// <summary>
    /// 任务权重配置
    /// </summary>
    protected readonly TaskWeightOptions TaskWeightOptions = taskWeightOptions ??
        throw new ArgumentNullException(nameof(taskWeightOptions))
        ;

    public abstract IAsyncEnumerable<SseEvent> Send(T request,
        CancellationToken cancellationToken = default)
        ;

    /// <summary>
    /// 验证任务权重配置是否合法
    /// </summary>
    /// <param name="totalWeights"></param>
    /// <exception cref="ArgumentException"></exception>
    protected void ValidateWeights(TaskWeightItem[] totalWeights)
    {
        int total = totalWeights.Sum(w => w.Weight);
        if (total != 100)
        {
            throw new ArgumentException("任务权重之和必须为 100");
        }

        if (totalWeights.Any(w => w.WeightType == TaskWeightType.None))
        {
            throw new ArgumentException("任务权重类型不能为 None");
        }

        if (totalWeights.Any(w => w.Weight <= 0))
        {
            throw new ArgumentException("任务权重必须大于 0");
        }

        var sb = new StringBuilder();
        sb.Append("使用子任务权重配置: ");
        sb.AppendFormat("{0}={1}%", totalWeights[0].WeightType, totalWeights[0].Weight);
        for(int i = 1; i < totalWeights.Length; i++)
        {
            sb.AppendFormat(", {0}={1}%", totalWeights[i].WeightType, totalWeights[i].Weight);
        }
        Logger.LogInformation(sb.ToString());
    }
}