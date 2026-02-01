namespace Ke.Tasks.Models;

/// <summary>
/// 任务权重配置
/// </summary>
public class TaskWeightOptions
{
    /// <summary>
    /// 语音识别任务权重配置
    /// </summary>
    public TaskWeightItem[] SpeechRecognize { get; set; } = [
        new TaskWeightItem
        {
            WeightType = TaskWeightType.Transcode,
            Weight = 20
        },
        new TaskWeightItem
        {
            WeightType = TaskWeightType.ASR,
            Weight = 80
        }
    ];
}

public class TaskWeightItem
{
    public TaskWeightType WeightType { get; set; } = TaskWeightType.None;
    public int Weight { get; set; } = 1;
}

/// <summary>
/// 任务权重类别
/// </summary>
public enum TaskWeightType
{
    None,
    Transcode,
    ASR,
    TTS,
    Translation
}

public static class TaskWeightTypeExtensions
{
    public static string ToFriendlyString(this TaskWeightType weightType)
    {
        return weightType switch
        {
            TaskWeightType.Transcode => "转码",
            TaskWeightType.ASR => "语音识别",
            TaskWeightType.TTS => "语音合成",
            TaskWeightType.Translation => "翻译",
            _ => "None"
        };
    }
}