using Cadence.Core.Models;
using Cadence.Core.Scheduling;
using Xunit;

namespace Cadence.Tests
{
    public class RoutineClockTests
    {
        private static RoutineClock BuildClock() => new(new[]
        {
            new Block(new TimeOnly(10, 0), "Wake & Anchor", BlockRole.Wake),
            new Block(new TimeOnly(13, 0), "Work Block (Portfolio)", BlockRole.Unspecified),
            new Block(new TimeOnly(20, 0), "Gaming", BlockRole.Unspecified),
            new Block(new TimeOnly(0, 0), "Art", BlockRole.Unspecified),
            new Block(new TimeOnly(2, 30), "Hard Stop Alarm", BlockRole.Unspecified),
            new Block(new TimeOnly(3, 0), "Sleep", BlockRole.Sleep),
        });

        private static DateTimeOffset At(int day, int hour, int minute) =>
            new(2026, 8, day, hour, minute, 0, TimeSpan.Zero);

        [Fact]
        public void MidDay_ReturnsActiveBlock()
            => Assert.Equal("Work Block (Portfolio)",
                BuildClock().GetCurrentBlock(At(19, 13, 30)).Block.Label);

        [Fact]
        public void PastMidnight_WrapsToPreviousEveningBlocks()
        {
            var clock = BuildClock();
            Assert.Equal("Art", clock.GetCurrentBlock(At(19, 1, 0)).Block.Label);
            Assert.Equal("Hard Stop Alarm", clock.GetCurrentBlock(At(19, 2, 45)).Block.Label);
            Assert.Equal("Sleep", clock.GetCurrentBlock(At(19, 9, 0)).Block.Label);
        }

        [Fact]
        public void CycleId_RollsOverAtWake_NotAtMidnight()
        {
            var clock = BuildClock();
            var evening   = clock.GetCurrentBlock(At(18, 23, 0));  // Gaming, Mon
            var lateNight = clock.GetCurrentBlock(At(19, 1, 0));   // Art, Tue 01:00
            var afterWake = clock.GetCurrentBlock(At(19, 13, 0));  // Work, Tue

            Assert.Equal(evening.CycleId, lateNight.CycleId);       // same cycle past midnight
            Assert.Equal(evening.CycleId + 1, afterWake.CycleId);   // rolls at wake
        }
    }
}