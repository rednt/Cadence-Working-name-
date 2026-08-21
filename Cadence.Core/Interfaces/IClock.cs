
namespace Cadence.Core.Interfaces
{
    public interface IClock
    {
        DateTimeOffset Now { get; }
    }
}