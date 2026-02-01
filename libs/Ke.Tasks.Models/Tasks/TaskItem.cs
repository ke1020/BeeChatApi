using System;
using System.Collections.Generic;

namespace Ke.Tasks.Models;

/// <summary>
/// 单个文件处理项
/// </summary>
public class TaskItem
{
    public string FileId { get; set; } = Guid.NewGuid().ToString();
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int TotalSteps { get; set; } = 5; // 转码准备、转码、转码完成、识别、识别完成
    public int CompletedSteps { get; set; } = 0;
    public double Progress => TotalSteps > 0 ? (double)CompletedSteps / TotalSteps * 100 : 0;
    public FileStatus Status { get; set; } = FileStatus.Pending;
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = [];
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}