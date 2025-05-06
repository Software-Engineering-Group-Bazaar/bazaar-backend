using System.ComponentModel.DataAnnotations;

namespace Inventory.Dtos
{
    public class CreateInventoryRequestDto
    {
        [Required(ErrorMessage = "Product ID is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Product ID must be a positive number.")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Store ID is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Store ID must be a positive number.")]
        public int StoreId { get; set; }

        [Required(ErrorMessage = "Initial quantity is required.")]
        [Range(0, int.MaxValue, ErrorMessage = "Initial quantity cannot be negative.")]
        public int InitialQuantity { get; set; }
    }
}