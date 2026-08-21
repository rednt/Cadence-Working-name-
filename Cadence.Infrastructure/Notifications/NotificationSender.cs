using Cadence.Core.Interfaces;
using Cadence.Core.Models;

namespace Cadence.Infrastructure.Notifications
{
    public sealed class ConsoleNotificationSender : INotificationSender
    {
        public Task SendAsync(NotificationType notificationType, string message, CancellationToken cancellationToken = default)
        {
            Console.ForegroundColor = notificationType == NotificationType.CycleRoll ? ConsoleColor.Yellow : ConsoleColor.Cyan;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{notificationType}] {message}");
            Console.ResetColor();
            return Task.CompletedTask;
        }
    }
}

