namespace Cadence.Core.Models
{
    public sealed class ContainerTaskCount
    {
        public string ContainerLabel { get; set; } = string.Empty;
        public int PendingCount { get; set; }

        public ContainerTaskCount() { }
        
        public ContainerTaskCount(string containerLabel, int pendingCount)
        {
            ContainerLabel = containerLabel;
            PendingCount = pendingCount;
        }

    }
}
