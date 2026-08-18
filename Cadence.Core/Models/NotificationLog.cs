using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cadence.Core.Models
{
    public class NotificationLog
    {
        public int Id { get ; set; }
        public DateTime FiredAt { get ; set; }
        public NotificationType Type { get ; set; }
        public bool Acknowledged { get ; set; }
        public int CycleId { get ; set; }

        public NotificationLog(){}
        public NotificationLog(int id, DateTime firedAt, NotificationType type, bool acknowledged, int cycleId)
        {
            Id = id;
            FiredAt = firedAt;
            Type = type;
            Acknowledged = acknowledged;
            CycleId = cycleId;
        }


    }
}