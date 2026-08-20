
namespace Cadence.Core.Models
{
    public class TaskItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ContainerLabel { get; set; } = string.Empty;
        public TimeOnly? DueAt { get; set; }
        public TaskStatus Status { get; set; }
        public TaskPriority Priority { get; set; }

        public TaskItem(){}
        public TaskItem(int id, string title, string containerLabel, TimeOnly? dueAt, TaskStatus status, TaskPriority priority)
        {
            Id = id;
            Title = title;
            ContainerLabel = containerLabel;
            DueAt = dueAt;
            Status = status;
            Priority = priority;
        }
    }
}