using Order.Models;

namespace Order.DTOs
{
    public class OrderSummaryDto
    {
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
        public OrderStatus Status { get; set; }
        public int StoreId { get; set; }
        public int ItemCount { get; set; }
        public string? BuyerName { get; set; }
    }
}
