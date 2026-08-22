using Cadence.Core.Models;
using TaskStatus = Cadence.Core.Models.TaskStatus;

namespace Cadence.Core.Interfaces
{
    public interface ICadenceStore
    {
        Task<TaskItem> AddTaskAsync(TaskItem task, CancellationToken ct = default);
        Task<IReadOnlyList<TaskItem>> GetTasksByContainerLabelAsync(string containerLabel, TaskStatus? status = null, CancellationToken ct = default);
        Task LogNotificationAsync(NotificationLog log, CancellationToken ct = default);
        Task<bool> CompleteTaskAsync(int id, CancellationToken ct = default);
    }
}