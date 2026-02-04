namespace Ke.Tasks.SSE.Models;

/// <summary>
/// 事件缓冲区统计信息
/// </summary>
public class EventBufferStatistics
{
    /// <summary>
    /// 事件总数
    /// </summary>
    public int TotalEvents { get; set; }
    /// <summary>
    /// 客户端总数
    /// </summary>
    public int TotalClients { get; set; }
    /// <summary>
    /// 队列长度
    /// </summary>
    public int QueueLength { get; set; }
    /// <summary>
    /// 最旧事件时间
    /// </summary>
    public long OldestEventTime { get; set; }
    /// <summary>
    /// 最新事件时间
    /// </summary>
    public long NewestEventTime { get; set; }
}