namespace Order.Models.DTOs
{
    public class CreateOrderRequestDto
    {
        public int StoreId { get; set; }
        public string Status { get; set; } = "Pending";
    }
}
