using System;

namespace Notifications.Dtos
{
    public class NotificationDto
    {
        public int Id { get; set; }
        public required string Message { get; set; }
        public bool IsRead { get; set; }
        public DateTime Timestamp { get; set; }
        public string? RelatedEntityType { get; set; }
        public int? RelatedEntityId { get; set; }
        public string? LinkUrl { get; set; }
    }
}