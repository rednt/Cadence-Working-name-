
namespace Cadence.Core.Models
{
    public class Block
    {
        public TimeOnly StartTime { get; set; }
        public string Label { get; set; } = string.Empty;
        public BlockRole Role { get; set; } = BlockRole.Unspecified;

        public Block() { }
        public Block(TimeOnly startTime, string label, BlockRole role)
        {
            StartTime = startTime;
            Label = label;
            Role = role;
        }


    }
}