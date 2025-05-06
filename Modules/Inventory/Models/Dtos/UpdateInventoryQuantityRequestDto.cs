using System.ComponentModel.DataAnnotations;

namespace Inventory.Dtos
{
    public class UpdateInventoryQuantityRequestDto
    {
        [Required(ErrorMessage = "Product ID is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Product ID must be a positive number.")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Store ID is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Store ID must be a positive number.")]
        public int StoreId { get; set; }

        [Required(ErrorMessage = "New quantity is required.")]
        [Range(0, int.MaxValue, ErrorMessage = "Quantity cannot be negative.")]
        public int NewQuantity { get; set; }
    }
}