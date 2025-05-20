using System.ComponentModel.DataAnnotations;

namespace Ticketing.Dtos
{
    public class CreateTicketDto
    {
        [Required(ErrorMessage = "Title is required.")]
        [MaxLength(255, ErrorMessage = "Title cannot exceed 255 characters.")]
        public required string Title { get; set; }

        [Required(ErrorMessage = "Description is required.")]
        [MinLength(10, ErrorMessage = "Description must be at least 10 characters long.")]
        public required string Description { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Order ID must be a positive number if provided.")]
        public int? OrderId { get; set; }
    }
}