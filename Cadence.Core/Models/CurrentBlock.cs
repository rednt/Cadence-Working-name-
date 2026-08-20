
namespace Cadence.Core.Models
{
    public class CurrentBlock
    {
        public Block Block { get; set; }
        public int CycleId { get; set; }

        public CurrentBlock(Block block, int cycleId)
        {
            Block = block;
            CycleId = cycleId;
        }
    }
}