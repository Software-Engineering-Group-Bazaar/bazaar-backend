using System.ComponentModel.DataAnnotations;
using Order.Models;

namespace Order.DTOs
{
    public class UpdateOrderStatusRequestDto
    {
        [Required]
        public int OrderId { get; set; }

        [Required]
        public required string NewStatus { get; set; }

    }
}