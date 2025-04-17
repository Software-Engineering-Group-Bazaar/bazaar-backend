namespace Order.Models.DTOs
{
    public class OrderResponseDto
    {
        public int Id { get; set; }
        public int StoreId { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
