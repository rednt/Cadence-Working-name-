using Cadence.Core.Interfaces;
using Cadence.Core.Models;
using TaskStatus = Cadence.Core.Models.TaskStatus;

namespace Cadence.Core.Scheduling
{
    public sealed class RuleEngine
    {
        private readonly IRoutineSource _routineSource;
        private readonly ICadenceStore _cadenceStore;
        private readonly INotificationSender _notificationSender;
        private readonly IClock _clock;

        private string? _lastBlockLabel;
        private int _lastCycleId;
        private bool _initialized;

        public RuleEngine(IRoutineSource routineSource, ICadenceStore cadenceStore, INotificationSender notificationSender, IClock clock)
        {
            _routineSource = routineSource;
            _cadenceStore = cadenceStore;
            _notificationSender = notificationSender;
            _clock = clock;
        }

        public async Task TickAsync(CancellationToken ct = default)
        {
            var now = _clock.Now;
            var currentBlock = _routineSource.GetCurrentBlock(now);
            var currentBlockLabel = currentBlock.Block.Label;
            var currentCycleId = currentBlock.CycleId;

            if (!_initialized)
            {
                _lastBlockLabel = currentBlockLabel;
                _lastCycleId = currentCycleId;
                _initialized = true;
                return;
            }

            bool blockChanged = currentBlockLabel != _lastBlockLabel;
            bool cycleRolled = currentCycleId != _lastCycleId;


            _lastBlockLabel = currentBlockLabel;
            _lastCycleId = currentCycleId;

            if (!blockChanged) return;
            if (currentBlock.Block.Role == BlockRole.Wake) return;

            var pendingTasks = await _cadenceStore.GetTasksByContainerLabelAsync(currentBlockLabel, status: TaskStatus.Pending, ct);
            string message = $"Entering '{currentBlockLabel}'. {pendingTasks.Count} tasks are pending.";


            await _notificationSender.SendAsync(NotificationType.BlockTransition, message, ct);
            await _cadenceStore.LogNotificationAsync(new NotificationLog
            {
                Type = NotificationType.BlockTransition,
                CycleId = currentCycleId,
                FiredAt = now
            }, ct);

            if (pendingTasks.Count > 0)
            {
                var taskList = string.Join(", ", pendingTasks.Select(t => t.Title));
                var taskMessage = $"Tasks for '{currentBlockLabel}': {taskList}";
                await _notificationSender.SendAsync(NotificationType.TaskSurfaced, taskMessage, ct);
                await _cadenceStore.LogNotificationAsync(new NotificationLog
                {
                    Type = NotificationType.TaskSurfaced,
                    CycleId = currentCycleId,
                    FiredAt = now
                }, ct);
            }

            if (cycleRolled)
            {
                await _cadenceStore.LogNotificationAsync(new NotificationLog
                {
                    Type = NotificationType.CycleRoll,
                    CycleId = currentCycleId,
                    FiredAt = now
                }, ct);
            }
        }
    }
}