namespace Order.Models.DTOs
{
    public class UpdateOrderStatusRequestDto
    {
        public int OrderId { get; set; }
        public string? NewStatus { get; set; }
    }
}