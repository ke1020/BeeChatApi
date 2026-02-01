namespace Ke.Tasks.SSE.Models;

/// <summary>
/// 事件缓冲区配置选项
/// </summary>
public class EventBufferOptions
{
    /// <summary>
    /// 最大缓冲区大小
    /// </summary>
    public int MaxBufferSize { get; set; } = 1000;

    /// <summary>
    /// 默认返回事件数量
    /// </summary>
    public int DefaultEventCount { get; set; } = 10;

    /// <summary>
    /// 每次请求最大事件数量
    /// </summary>
    public int MaxEventsPerRequest { get; set; } = 100;

    /// <summary>
    /// 事件默认最大年龄（分钟）
    /// </summary>
    public int DefaultEventMaxAgeInMinutes { get; set; } = 60;

    /// <summary>
    /// 客户端闲置超时时间（分钟）
    /// </summary>
    public int ClientInactiveTimeoutInMinutes { get; set; } = 30;

    /// <summary>
    /// 排序阈值，超过此数量使用索引排序
    /// </summary>
    public int SortThreshold { get; set; } = 100;

    /// <summary>
    /// 清理间隔（分钟）
    /// </summary>
    public int CleanupIntervalInMinutes { get; set; } = 5;

    /// <summary>
    /// 是否启用自动清理
    /// </summary>
    public bool EnableAutoCleanup { get; set; } = true;
}