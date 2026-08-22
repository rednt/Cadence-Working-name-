using Cadence.Core.Interfaces;
using Cadence.Core.Models;
using Microsoft.EntityFrameworkCore;
using TaskStatus = Cadence.Core.Models.TaskStatus;
namespace Cadence.Infrastructure.Persistence
{
    public sealed class SqliteCadenceStore : ICadenceStore
    {
        private readonly CadenceDbContext _db;

        public SqliteCadenceStore(CadenceDbContext db)
        {
            _db = db;
        }

        public async Task<TaskItem> AddTaskAsync(TaskItem task, CancellationToken ct = default)
        {
            _db.Tasks.Add(task);
            await _db.SaveChangesAsync(ct);
            return task;
        }

        public async Task<IReadOnlyList<TaskItem>> GetTasksByContainerLabelAsync(string containerLabel, TaskStatus? status = null, CancellationToken ct = default)
        {
            return await _db.Tasks
                .Where(t => t.ContainerLabel == containerLabel && (status == null || t.Status == status))
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.Id)
                .ToListAsync(ct);
        }

        public async Task LogNotificationAsync(NotificationLog log, CancellationToken ct = default)
        {
            _db.NotificationLogs.Add(log);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<bool> CompleteTaskAsync(int id, CancellationToken ct = default)
        {
            var task = await _db.Tasks.FindAsync(new object[] { id }, ct);
            if (task is null)
            {
                return false;
            }
            task.Status = TaskStatus.Completed;
            await _db.SaveChangesAsync(ct);
            return true;
        }

    }
}