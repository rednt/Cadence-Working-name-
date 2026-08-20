using Cadence.Core.Models;

namespace Cadence.Core.Interfaces
{
    public interface INotificationSender
    {
        Task SendAsync(NotificationType notificationType, string message, CancellationToken cancellationToken = default);
    }
}