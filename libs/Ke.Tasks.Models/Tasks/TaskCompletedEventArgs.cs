namespace Ke.Tasks.Models;

public class TaskCompletedEventArgs : EventArgs
{
    public TaskCompletedEventArgs(TaskInfo task, TaskStatus status)
    {
        Task = task;
        Status = status;
    }

    public TaskInfo Task { get; }
    public TaskStatus Status { get; }
}

public class TaskItemCompletedEventArgs(TaskStatus status, TaskItem task) : EventArgs
{
    public TaskStatus Status { get; } = status;
    public TaskItem Task { get; set; } = task;
}