using System;

namespace Inventory.Dtos
{
    public class InventoryDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int StoreId { get; set; }
        public string? ProductName { get; set; }
        public int Quantity { get; set; }
        public bool OutOfStock { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}