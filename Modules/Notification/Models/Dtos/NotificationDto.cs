using System;
using System.ComponentModel.DataAnnotations;

namespace Notifications.Dtos
{
    public class NotificationDto
    {
        public int Id { get; set; }
        public required string Message { get; set; }
        public bool IsRead { get; set; }
        public DateTime Timestamp { get; set; }
        public int? OrderId { get; set; }
    }

    public class CreateNotificationRequestDto
    {
        [Required]
        [MaxLength(450)]
        public required string UserId { get; set; }

        [Required]
        [MaxLength(500)]
        public required string Message { get; set; }

    }
}