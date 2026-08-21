using Cadence.Core.Interfaces;
namespace Cadence.Core.Scheduling;


public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}