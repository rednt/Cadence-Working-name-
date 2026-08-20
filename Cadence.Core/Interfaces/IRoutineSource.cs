using Cadence.Core.Models;

namespace Cadence.Core.Interfaces
{
    public interface IRoutineSource
    {
        IReadOnlyList<Block> Blocks { get; }
        CurrentBlock GetCurrentBlock(DateTimeOffset now);
    }
}