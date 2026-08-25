

namespace Cadence.Core.Models
{
        public sealed class Heartbeat
        {
            public int WorkerId { get; set; } // Always 1 since singleton row
            public DateTimeOffset LastTickAt { get; set; }
        }
    
}