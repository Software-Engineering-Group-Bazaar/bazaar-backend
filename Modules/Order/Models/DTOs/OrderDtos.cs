using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Order.Models; // Za OrderStatus enum

namespace Order.DTOs
{
    // --- DTOs for Creating/Updating Orders ---

    // DTO za kreiranje nove narudžbe (od strane Buyera)
    public class CreateOrderRequestDto
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Store ID must be valid.")]
        public int StoreId { get; set; }

        // Opciono: Lista stavki se može poslati odmah prilikom kreiranja narudžbe
        // public List<CreateOrderItemDto> Items { get; set; } = new List<CreateOrderItemDto>();
    }

    // DTO za prikaz punih detalja narudžbe
    public class OrderDetailDto
    {
        public int Id { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = string.Empty; // Status kao string
        public decimal TotalAmount { get; set; }
        public int StoreId { get; set; }
        // Informacije o kupcu (može biti poseban DTO)
        public OrderUserInfoDto? BuyerInfo { get; set; }
        // Informacije o adresi dostave (ako postoji)
        public ShippingAddressDto? ShippingAddress { get; set; } // String ili strukturirano?
        // Lista stavki narudžbe
        public List<OrderItemDto> Items { get; set; } = new List<OrderItemDto>();
    }

    // Pomoćni DTO za informacije o korisniku (možda već postoji u Users modulu?)
    public class OrderUserInfoDto
    {
        public string Id { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? Email { get; set; }
        // Dodaj ostala polja po potrebi (npr. telefon)
    }

    // Pomoćni DTO za adresu dostave (ako je strukturirana)
    public class ShippingAddressDto
    {
        public string StreetAndNumber { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Municipality { get; set; } = string.Empty;
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
    }
}