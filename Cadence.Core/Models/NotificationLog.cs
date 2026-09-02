
namespace Cadence.Core.Models
{
    public class NotificationLog
    {
        public int Id { get; set; }
        public DateTimeOffset FiredAt { get; set; }
        public NotificationType Type { get; set; }
        public bool Acknowledged { get; set; }
        public int CycleId { get; set; }

        public NotificationLog() { }
        public NotificationLog(int id, DateTimeOffset firedAt, NotificationType type, bool acknowledged, int cycleId)
        {
            Id = id;
            FiredAt = firedAt;
            Type = type;
            Acknowledged = acknowledged;
            CycleId = cycleId;
        }


    }
}