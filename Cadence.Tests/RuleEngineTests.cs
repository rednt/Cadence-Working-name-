using Cadence.Core.Interfaces;
using Cadence.Core.Models;
using Cadence.Core.Scheduling;
using TaskStatus = Cadence.Core.Models.TaskStatus;

namespace Cadence.Tests
{
    public sealed class RuleEngineTests
    {
        private static readonly Block WakeBlock = new(new TimeOnly(10, 0), "Wake", BlockRole.Wake);
        private static readonly Block WorkBlock = new(new TimeOnly(13, 0), "Work", BlockRole.Unspecified);
        private static readonly Block ArtBlock = new(new TimeOnly(0, 0), "Art", BlockRole.Unspecified);
        private static readonly Block SleepBlock = new(new TimeOnly(3, 0), "Sleep", BlockRole.Sleep);

        private static CurrentBlock CurrentAt(Block block, int cycleId = 1)
            => new(block, cycleId);

        // ──────────────────────────── Tick 1: initialization ────────────────────────────

        [Fact]
        public async Task Tick_Initialization_SetsStateWithoutNotification()
        {
            var routine = new MockRoutineSource(CurrentAt(WorkBlock));
            var store = new MockCadenceStore();
            var sender = new MockNotificationSender();
            var clock = new FakeClock(new DateTimeOffset(2026, 8, 19, 13, 0, 0, TimeSpan.Zero));

            var engine = new RuleEngine(routine, store, sender, clock);
            await engine.TickAsync();

            Assert.Empty(sender.Sent);
            Assert.Empty(store.Logs);
        }

        // ──────────────────────────── Tick 2+: block changed ────────────────────────────

        [Fact]
        public async Task Tick_BlockChanged_FiresTransitionNotification()
        {
            var routine = new MockRoutineSource(CurrentAt(WorkBlock));
            var sender = new MockNotificationSender();
            var store = new MockCadenceStore();
            var clock = new FakeClock(new DateTimeOffset(2026, 8, 19, 13, 0, 0, TimeSpan.Zero));
            var engine = new RuleEngine(routine, store, sender, clock);

            await engine.TickAsync(); // init => "Work"

            routine.Current = CurrentAt(ArtBlock); // advance to "Art"
            await engine.TickAsync();

            var transition = sender.Sent.Where(n => n.Type == NotificationType.BlockTransition).ToList();
            Assert.Single(transition);
            Assert.Contains("Art", transition[0].Message);
        }

        // ──────────────────────────── Tick 2+: same block ────────────────────────────

        [Fact]
        public async Task Tick_SameBlock_NoNotification()
        {
            var routine = new MockRoutineSource(CurrentAt(WorkBlock));
            var sender = new MockNotificationSender();
            var store = new MockCadenceStore();
            var clock = new FakeClock(new DateTimeOffset(2026, 8, 19, 13, 0, 0, TimeSpan.Zero));
            var engine = new RuleEngine(routine, store, sender, clock);

            await engine.TickAsync(); // init => "Work"
            await engine.TickAsync(); // still "Work"

            Assert.Empty(sender.Sent);
        }

        // ──────────────────────────── Tick 2+: cycle rolled ────────────────────────────

        [Fact]
        public async Task Tick_CycleRolled_IncludesNewDayMessage()
        {
            var routine = new MockRoutineSource(CurrentAt(WorkBlock, cycleId: 1));
            var sender = new MockNotificationSender();
            var store = new MockCadenceStore();
            var clock = new FakeClock(new DateTimeOffset(2026, 8, 19, 13, 0, 0, TimeSpan.Zero));
            var engine = new RuleEngine(routine, store, sender, clock);

            await engine.TickAsync(); // init => cycleId 1

            routine.Current = CurrentAt(ArtBlock, cycleId: 2); // next day
            await engine.TickAsync();

            var transition = sender.Sent.Where(n => n.Type == NotificationType.BlockTransition).ToList();
            Assert.Single(transition);

            Assert.Contains(store.Logs, l => l.Type == NotificationType.CycleRoll);
        }

        // ──────────────────────────── Tick 2+: wake block skipped ────────────────────────────

        [Fact]
        public async Task Tick_WakeBlock_SkipsNotification()
        {
            var routine = new MockRoutineSource(CurrentAt(WorkBlock));
            var sender = new MockNotificationSender();
            var store = new MockCadenceStore();
            var clock = new FakeClock(new DateTimeOffset(2026, 8, 19, 13, 0, 0, TimeSpan.Zero));
            var engine = new RuleEngine(routine, store, sender, clock);

            await engine.TickAsync(); // init => "Work"

            routine.Current = CurrentAt(WakeBlock); // next cycle's wake
            await engine.TickAsync();

            Assert.Empty(sender.Sent);
        }

        // ──────────────────────────── Tick 2+: pending tasks ────────────────────────────

        [Fact]
        public async Task Tick_PendingTasksExist_FiresTaskSurfacedNotification()
        {
            var routine = new MockRoutineSource(CurrentAt(WorkBlock));
            var sender = new MockNotificationSender();
            var store = new MockCadenceStore();
            var clock = new FakeClock(new DateTimeOffset(2026, 8, 19, 13, 0, 0, TimeSpan.Zero));
            var engine = new RuleEngine(routine, store, sender, clock);

            await engine.TickAsync(); // init => "Work"

            store.Tasks["Art"] = new List<TaskItem>
            {
                new() { Id = 1, Title = "Sketch layout", ContainerLabel = "Art", Status = TaskStatus.Pending },
                new() { Id = 2, Title = "Ink outlines", ContainerLabel = "Art", Status = TaskStatus.Pending },
            };

            routine.Current = CurrentAt(ArtBlock);
            await engine.TickAsync();

            var surfaced = sender.Sent.Where(n => n.Type == NotificationType.TaskSurfaced).ToList();
            Assert.Single(surfaced);
            Assert.Contains("Sketch layout", surfaced[0].Message);
            Assert.Contains("Ink outlines", surfaced[0].Message);
        }

        // ──────────────────────────── Tick 2+: no tasks ────────────────────────────

        [Fact]
        public async Task Tick_NoTasks_NoTaskSurfacedNotification()
        {
            var routine = new MockRoutineSource(CurrentAt(WorkBlock));
            var sender = new MockNotificationSender();
            var store = new MockCadenceStore();
            var clock = new FakeClock(new DateTimeOffset(2026, 8, 19, 13, 0, 0, TimeSpan.Zero));
            var engine = new RuleEngine(routine, store, sender, clock);

            await engine.TickAsync(); // init => "Work"

            routine.Current = CurrentAt(ArtBlock);
            await engine.TickAsync();

            Assert.Contains(sender.Sent, n => n.Type == NotificationType.BlockTransition);
            Assert.DoesNotContain(sender.Sent, n => n.Type == NotificationType.TaskSurfaced);
        }

        // ──────────────────────────── audit: every notification logged ────────────────────────────

        [Fact]
        public async Task Tick_EveryNotification_LoggedToStore()
        {
            var routine = new MockRoutineSource(CurrentAt(WorkBlock));
            var sender = new MockNotificationSender();
            var store = new MockCadenceStore();
            var clock = new FakeClock(new DateTimeOffset(2026, 8, 19, 13, 0, 0, TimeSpan.Zero));
            var engine = new RuleEngine(routine, store, sender, clock);

            await engine.TickAsync(); // init

            store.Tasks["Art"] = new List<TaskItem>
            {
                new() { Id = 1, Title = "Paint", ContainerLabel = "Art", Status = TaskStatus.Pending },
            };

            routine.Current = CurrentAt(ArtBlock, cycleId: 2); // block change + cycle roll
            await engine.TickAsync();

            // 3 notifications sent: BlockTransition + TaskSurfaced
            // 3 logs written: BlockTransition + TaskSurfaced + CycleRoll
            Assert.Equal(sender.Sent.Count, store.Logs.Count(l => l.Type != NotificationType.CycleRoll));

            foreach (var notification in sender.Sent)
            {
                Assert.Contains(store.Logs, l => l.Type == notification.Type);
            }
        }

        // ═══════════════════════════════ mock helpers ═══════════════════════════════

        private sealed class MockRoutineSource : IRoutineSource
        {
            public CurrentBlock Current { get; set; }
            public IReadOnlyList<Block> Blocks => Array.Empty<Block>();

            public MockRoutineSource(CurrentBlock initial) => Current = initial;

            public CurrentBlock GetCurrentBlock(DateTimeOffset now) => Current;
        }

        private sealed class MockCadenceStore : ICadenceStore
        {
            public Dictionary<string, List<TaskItem>> Tasks { get; } = new();
            public List<NotificationLog> Logs { get; } = new();

            public Task<TaskItem> AddTaskAsync(TaskItem task, CancellationToken ct = default)
            {
                if (!Tasks.ContainsKey(task.ContainerLabel))
                    Tasks[task.ContainerLabel] = new List<TaskItem>();
                Tasks[task.ContainerLabel].Add(task);
                return Task.FromResult(task);
            }

            public Task<IReadOnlyList<TaskItem>> GetTasksByContainerLabelAsync(
                string containerLabel, TaskStatus? status = null, CancellationToken ct = default)
            {
                if (!Tasks.TryGetValue(containerLabel, out var list))
                    return Task.FromResult<IReadOnlyList<TaskItem>>(Array.Empty<TaskItem>());

                var filtered = status is null
                    ? list
                    : list.Where(t => t.Status == status.Value).ToList();

                return Task.FromResult<IReadOnlyList<TaskItem>>(filtered);
            }

            public Task LogNotificationAsync(NotificationLog log, CancellationToken ct = default)
            {
                Logs.Add(log);
                return Task.CompletedTask;
            }
            public Task<bool> CompleteTaskAsync(int id, CancellationToken ct = default)
            {
                foreach (var tasks in Tasks.Values)
                {
                    var task = tasks.FirstOrDefault(t => t.Id == id);
                    if (task is not null)
                    {
                        task.Status = TaskStatus.Completed;
                        return Task.FromResult(true);
                    }
                }
                return Task.FromResult(false);
            }
            public Task<bool> ModifyTaskAsync(int id, string newTitle, CancellationToken ct = default)
            {
                foreach (var tasks in Tasks.Values)
                {
                    var task = tasks.FirstOrDefault(t => t.Id == id);
                    if (task is not null)
                    {
                        task.Title = newTitle;
                        return Task.FromResult(true);
                    }
                }
                return Task.FromResult(false);
            }
            public Task<IReadOnlyList<ContainerTaskCount>> GetContainerTaskCountsAsync(CancellationToken ct = default)
            {
                var counts = Tasks.Select(kvp => new ContainerTaskCount(kvp.Key, kvp.Value.Count(t => t.Status == TaskStatus.Pending)))
                                  .ToList();
                return Task.FromResult<IReadOnlyList<ContainerTaskCount>>(counts);
            }
            public Task RecordHeartbeatAsync(DateTimeOffset timestamp, CancellationToken ct = default)
            {
                return Task.CompletedTask;
            }

            public Task<DateTimeOffset?> GetLastHeartbeatAsync(CancellationToken ct = default)
            {
                return Task.FromResult<DateTimeOffset?>(null);
            }
        }

        private sealed class MockNotificationSender : INotificationSender
        {
            public List<(NotificationType Type, string Message)> Sent { get; } = new();

            public Task SendAsync(NotificationType notificationType, string message, CancellationToken ct = default)
            {
                Sent.Add((notificationType, message));
                return Task.CompletedTask;
            }
        }

        private sealed class FakeClock : IClock
        {
            public DateTimeOffset Now { get; set; }
            public FakeClock(DateTimeOffset now) => Now = now;
        }

        
    }
}
