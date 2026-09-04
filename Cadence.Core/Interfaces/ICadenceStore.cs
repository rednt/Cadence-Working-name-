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
        Task<bool> ModifyTaskAsync(int id, string? newTitle = null, TaskPriority? newPriority = null, CancellationToken ct = default);
        Task<IReadOnlyList<ContainerTaskCount>> GetContainerTaskCountsAsync(CancellationToken ct = default);
        Task RecordHeartbeatAsync(DateTimeOffset timestamp, CancellationToken ct = default);
        Task<DateTimeOffset?> GetLastHeartbeatAsync(CancellationToken ct = default);
        Task<bool> DeleteTaskAsync(int id, CancellationToken ct = default);
    }
}