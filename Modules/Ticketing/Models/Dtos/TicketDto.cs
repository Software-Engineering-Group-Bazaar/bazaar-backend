using System;

namespace Ticketing.Dtos
{
    public class TicketDto
    {
        public int Id { get; set; }
        public required string Title { get; set; }
        public required string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public required string UserId { get; set; }
        public string? UserUsername { get; set; }
        public string? AssignedAdminId { get; set; }
        public string? AdminUsername { get; set; }
        public int? ConversationId { get; set; }
        public int? OrderId { get; set; }
        public required string Status { get; set; }
        public bool IsResolved { get; set; }
    }
}