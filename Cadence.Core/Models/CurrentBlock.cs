using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cadence.Core.Models
{
    public class CurrentBlock
    {
        public Block Block { get ; set; }
        public int CycleId { get ; set; }

        public CurrentBlock(Block block, int cycleId)
        {
            Block = block;
            CycleId = cycleId;
        }
    }
}