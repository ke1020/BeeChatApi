using System;
using System.Collections.Generic;
using System.Linq;

namespace Ke.Tasks.Models;

/// <summary>
/// 任务信息
/// </summary>
public class TaskInfo
{
    public string TaskId { get; set; } = Guid.NewGuid().ToString();
    public string TaskName { get; set; } = string.Empty;
    public string[] Files { get; set; } = [];
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    /*
    public int CurrentFileIndex { get; set; } = 0;
    public int CurrentStepInFile { get; set; } = 0;
    public int TotalSteps => Files.Sum(f => f.TotalSteps);
    public int CompletedSteps => Files.Sum(f => f.CompletedSteps);
    public double OverallProgress => TotalSteps > 0 ? (double)CompletedSteps / TotalSteps * 100 : 0;
    /// <summary>
    /// 任务进度百分比
    /// </summary>
    public double Percentage { get; set; } = 0;
    */
}