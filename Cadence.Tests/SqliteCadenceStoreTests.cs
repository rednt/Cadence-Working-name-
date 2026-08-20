using Cadence.Core.Models;
using Cadence.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskStatus = Cadence.Core.Models.TaskStatus;

namespace Cadence.Tests
{
    public sealed class SqliteCadenceStoreTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly CadenceDbContext _db;
        private readonly SqliteCadenceStore _store;

        public SqliteCadenceStoreTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<CadenceDbContext>()
                .UseSqlite(_connection)
                .Options;
            _db = new CadenceDbContext(options);
            _db.Database.EnsureCreated();
            _store = new SqliteCadenceStore(_db);
        }

        public void Dispose()
        {
            _db.Dispose();
            _connection.Dispose();
        }

        private static TaskItem Task(string container, TaskStatus status = TaskStatus.Pending, TaskPriority priority = TaskPriority.Normal)
            => new() { Title = "Task", ContainerLabel = container, Status = status, Priority = priority };

        [Fact]
        public async Task AddTaskAsync_ReturnsTaskWithGeneratedId()
        {
            var task = Task("Work Block (Portfolio)");

            var saved = await _store.AddTaskAsync(task);

            Assert.Equal(task, saved);
            Assert.NotEqual(0, saved.Id);
        }

        [Fact]
        public async Task AddedTask_IsVisibleInFreshContextOverSameConnection()
        {
            var saved = await _store.AddTaskAsync(Task("Art"));

            var options = new DbContextOptionsBuilder<CadenceDbContext>()
                .UseSqlite(_connection)
                .Options;
            using var fresh = new CadenceDbContext(options);

            var reloaded = await fresh.Tasks.SingleAsync(t => t.Id == saved.Id);
            Assert.Equal("Art", reloaded.ContainerLabel);
            Assert.Equal(TaskStatus.Pending, reloaded.Status);
        }

        [Fact]
        public async Task GetByContainer_FiltersByContainerAndStatus()
        {
            await _store.AddTaskAsync(Task("Art"));                                            // pending, Art
            await _store.AddTaskAsync(Task("Art", TaskStatus.Completed));   // completed, Art
            await _store.AddTaskAsync(Task("Gaming"));                                          // pending, Gaming

            var all = await _store.GetTasksByContainerLabelAsync("Art", status: null);
            Assert.Equal(2, all.Count);

            var pending = await _store.GetTasksByContainerLabelAsync("Art", status: TaskStatus.Pending);
            Assert.Single(pending);
            Assert.Equal(TaskStatus.Pending, pending[0].Status);

            var completed = await _store.GetTasksByContainerLabelAsync("Art", status: TaskStatus.Completed);
            Assert.Single(completed);
            Assert.Equal(TaskStatus.Completed, completed[0].Status);

            var other = await _store.GetTasksByContainerLabelAsync("Sleep");
            Assert.Empty(other);
        }

        [Fact]
        public async Task GetByContainer_OrdersByPriorityDescThenId()
        {
            var high1 = await _store.AddTaskAsync(Task("Art", priority: TaskPriority.High));
            var low = await _store.AddTaskAsync(Task("Art", priority: TaskPriority.Low));
            var high2 = await _store.AddTaskAsync(Task("Art", priority: TaskPriority.High));
            var normal = await _store.AddTaskAsync(Task("Art", priority: TaskPriority.Normal));

            var result = await _store.GetTasksByContainerLabelAsync("Art");

            Assert.Equal(new[] { high1.Id, high2.Id, normal.Id, low.Id }, result.Select(t => t.Id).ToArray());
        }

        [Fact]
        public async Task LogNotificationAsync_PersistsFields()
        {
            var firedAt = new DateTimeOffset(2026, 8, 20, 13, 0, 0, TimeSpan.Zero);
            await _store.LogNotificationAsync(new NotificationLog
            {
                Type = NotificationType.TaskSurfaced,
                CycleId = 42,
                FiredAt = firedAt
            });

            var options = new DbContextOptionsBuilder<CadenceDbContext>()
                .UseSqlite(_connection)
                .Options;
            using var fresh = new CadenceDbContext(options);

            var reloaded = await fresh.NotificationLogs.SingleAsync();
            Assert.Equal(NotificationType.TaskSurfaced, reloaded.Type);
            Assert.Equal(42, reloaded.CycleId);
            Assert.Equal(firedAt, reloaded.FiredAt);
            Assert.False(reloaded.Acknowledged);
        }
    }
}