using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cadence.Core.Models;

namespace Cadence.Core.Interfaces
{
    public interface INotificationSender
    {
        Task SendAsync(NotificationType notificationType);
    }
}