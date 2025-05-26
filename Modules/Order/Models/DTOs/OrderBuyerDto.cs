using System.ComponentModel.DataAnnotations;
using Order.Models;

namespace Order.Models.DTOs.Buyer
{
    // DTO for representing an OrderItem in responses
    public class OrderItemGetBuyerDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }

        // Maybe add ProductName here if needed (requires joining or separate lookup)
        public decimal Price { get; set; }
        public int Quantity { get; set; }
    }

    // DTO for representing an Order in responses (GET requests)
    public class OrderGetBuyerDto
    {
        public int Id { get; set; }
        public string BuyerId { get; set; } = string.Empty;

        // Maybe add BuyerUserName/Email here (requires user lookup)
        public int StoreId { get; set; }

        // Maybe add StoreName here (requires store lookup)
        public string Status { get; set; } = string.Empty;
        public DateTime Time { get; set; }
        public decimal? Total { get; set; }
        public List<OrderItemGetBuyerDto> OrderItems { get; set; } =
            new List<OrderItemGetBuyerDto>();
        public int AddressId { get; set; }
    }

    // DTO for creating a new Order (POST request body)
    public class OrderCreateBuyerDto
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "StoreId must be a positive integer.")]
        public int StoreId { get; set; }

        public List<OrderItemGetBuyerDto> OrderItems { get; set; } =
            new List<OrderItemGetBuyerDto>();
        public int AddressId { get; set; }

        public bool UsingPoints { get; set; } = false;
    }
}
