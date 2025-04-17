using System.ComponentModel.DataAnnotations;

namespace Order.Models.DTOs
{
    public class UpdateOrderStatusRequestDto
    {
        public int OrderId { get; set; }

        [Required(ErrorMessage = "NewStatus je obavezan!")]
        public required string NewStatus { get; set; }
    }
}