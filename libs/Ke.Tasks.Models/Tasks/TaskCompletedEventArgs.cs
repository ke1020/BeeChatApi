namespace Ke.Tasks.Models;

public class TaskCompletedEventArgs(TaskInfo task) : EventArgs
{
    public TaskInfo Task { get; } = task;
}

public class TaskItemCompletedEventArgs(TaskItem task) : EventArgs
{
    public TaskItem Task { get; set; } = task;
}