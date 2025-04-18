using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Notifications.Models
{
    public class Notification
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(450)]
        public required string UserId { get; set; }
        [Required]
        [MaxLength(500)]
        public required string Message { get; set; } // Tekst notifikacije

        public bool IsRead { get; set; } = false; // Status čitanja

        public DateTime Timestamp { get; set; } = DateTime.UtcNow; // Vrijeme kreiranja

        [MaxLength(50)]
        public string? RelatedEntityType { get; set; } // Npr. "Order"

        public int? RelatedEntityId { get; set; } // Npr. OrderId
    }
}