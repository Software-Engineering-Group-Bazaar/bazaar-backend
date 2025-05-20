// In AdminApi.DTOs namespace or a sub-namespace
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Order.Models; // Assuming OrderStatus is here

namespace AdminApi.DTOs
{
    // DTO for representing an OrderItem in responses
    public class OrderItemGetDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        // Maybe add ProductName here if needed (requires joining or separate lookup)
        public decimal Price { get; set; }
        public int Quantity { get; set; }
    }

    // DTO for representing an Order in responses (GET requests)
    public class OrderGetDto
    {
        public int Id { get; set; }
        public string BuyerId { get; set; } = string.Empty;
        // Maybe add BuyerUserName/Email here (requires user lookup)
        public int StoreId { get; set; }
        // Maybe add StoreName here (requires store lookup)
        public string Status { get; set; }
        public DateTime Time { get; set; }
        public decimal? Total { get; set; }
        public List<OrderItemGetDto> OrderItems { get; set; } = new List<OrderItemGetDto>();
        public int AddressId { get; set; }
        public bool AdminDelivery { get; set; }
        public DateTime? ExpectedReadyAt { get; set; }
    }

    // DTO for creating a new Order (POST request body)
    public class OrderCreateDto
    {
        [Required]
        public string BuyerId { get; set; } = string.Empty;

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "StoreId must be a positive integer.")]
        public int StoreId { get; set; }

        public List<OrderItemGetDto> OrderItems { get; set; } = new List<OrderItemGetDto>();
        public int AddressId { get; set; } = 0;
        // Note: OrderItems are typically added separately after the order header is created.
        // If you need to create items simultaneously, this DTO would need an OrderItems list.
    }
    public class OrderUpdateDto
    {
        public string? BuyerId { get; set; }
        public int? StoreId { get; set; }
        public string? Status { get; set; }
        public DateTime? Time { get; set; }
        public decimal? Total { get; set; }
        public List<OrderItemGetDto>? OrderItems { get; set; }
    }

    public class OrderItemUpdateDto
    {
        public int Id { get; set; }
        public int? ProductId { get; set; }
        public decimal? Price { get; set; }
        public int? Quantity { get; set; }
    }

    // DTO for updating the status of an Order (PUT request body)
    public class OrderUpdateStatusDto
    {
        [Required]
        [EnumDataType(typeof(OrderStatus))] // Ensure valid enum value
        public string NewStatus { get; set; }
        public bool AdminDelivery { get; set; } = false;
        public int EstimatedPreparationTimeInMinutes { get; set; } = 0;
    }
}