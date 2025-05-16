using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ticketing.Models
{
    public class Ticket
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public required string Title { get; set; }

        [Required]
        public required string Description { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ResolvedAt { get; set; }

        [Required]
        [MaxLength(450)]
        public required string UserId { get; set; }

        [MaxLength(450)]
        public string? AssignedAdminId { get; set; }

        public int? ConversationId { get; set; }

        public int? OrderId { get; set; }

        [Required]
        [MaxLength(50)]
        public required string Status { get; set; } = TicketStatus.Requested.ToString();
    }
}