namespace Loyalty.Models
{
    public class Transaction
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public int OrderId { get; set; }
        public int PointsQuantity { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int StoreId { get; set; }
        public TransactionType TransactionType { get; set; }
    }
}