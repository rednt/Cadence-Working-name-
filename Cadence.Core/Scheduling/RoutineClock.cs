using Cadence.Core.Interfaces;
using Cadence.Core.Models;

namespace Cadence.Core.Scheduling
{
    public sealed class RoutineClock : IRoutineSource
    {
        private readonly IReadOnlyList<Block> _blocks;
        private readonly TimeSpan[] _offsets;
        private readonly TimeOnly _wakeStart;

        public IReadOnlyList<Block> Blocks => _blocks;

        public RoutineClock(IReadOnlyList<Block> blocks)
        {
            ArgumentNullException.ThrowIfNull(blocks);

            if (blocks.Count == 0)
            {
                throw new InvalidOperationException("Routine must define at least one block.");
            }

            var wakeBlocks = blocks.Where(b => b.Role == BlockRole.Wake).ToList();
            if (wakeBlocks.Count != 1)
            {
                throw new InvalidOperationException("Routine must define exactly one 'wake' block.");
            }

            var sleepBlocks = blocks.Where(b => b.Role == BlockRole.Sleep).ToList();
            if (sleepBlocks.Count != 1)
            {
                throw new InvalidOperationException("Routine must define exactly one 'sleep' block.");
            }

            _wakeStart = wakeBlocks[0].StartTime;

            _blocks = blocks.OrderBy(b => OffsetSinceWake(b.StartTime)).ToList();

            var duplicateTime = _blocks
                .GroupBy(b => b.StartTime)
                .FirstOrDefault(g => g.Count() > 1);
            if (duplicateTime is not null)
            {
                throw new InvalidOperationException(
                    $"Duplicate block start time '{duplicateTime.Key:HH:mm}' — start times must be unique.");
            }

            if (_blocks[0].Role != BlockRole.Wake)
            {
                throw new InvalidOperationException("The 'wake' block must anchor the start of the day.");
            }

            if (_blocks[^1].Role != BlockRole.Sleep)
            {
                throw new InvalidOperationException(
                    "Routine must not define blocks after the 'sleep' block — they would fall inside the sleep window.");
            }

            _offsets = _blocks.Select(b => OffsetSinceWake(b.StartTime)).ToArray();
        }

        public CurrentBlock GetCurrentBlock(DateTimeOffset now)
        {
            var elapsedNow = OffsetSinceWake(TimeOnly.FromDateTime(now.DateTime));

            var activeIndex = 0;
            for (var i = _offsets.Length - 1; i >= 0; i--)
            {
                if (_offsets[i] <= elapsedNow)
                {
                    activeIndex = i;
                    break;
                }
            }

            return new CurrentBlock(_blocks[activeIndex], ComputeCycleId(now));
        }

        private TimeSpan OffsetSinceWake(TimeOnly time)
        {
            var offset = time - _wakeStart;
            return offset < TimeSpan.Zero ? offset + TimeSpan.FromHours(24) : offset;
        }

        private int ComputeCycleId(DateTimeOffset now)
        {
            var today = DateOnly.FromDateTime(now.DateTime);
            var anchor = new DateTimeOffset(today.ToDateTime(_wakeStart), now.Offset);
            if (now < anchor)
            {
                anchor = anchor.AddDays(-1);
            }

            return DateOnly.FromDateTime(anchor.DateTime).DayNumber;
        }
    }
}