using System.ComponentModel.DataAnnotations;

namespace Order.DTOs
{
    // DTO za kreiranje nove stavke narudžbe
    public class CreateOrderItemDto
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Product ID must be valid.")]
        public int ProductId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; }

    }

    // DTO za ažuriranje postojeće stavke narudžbe
    public class UpdateOrderItemDto
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Product ID must be valid.")]
        public int ProductId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; }
    }

    // DTO za prikazivanje stavke narudžbe
    public class OrderItemDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal PricePerProduct { get; set; }
        public decimal Subtotal { get; set; }
        public string? ProductImageUrl { get; set; }
    }
}