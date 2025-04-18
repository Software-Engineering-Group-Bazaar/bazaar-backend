namespace Order.DTOs
{
    public class OrderSummaryDto
    {
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
