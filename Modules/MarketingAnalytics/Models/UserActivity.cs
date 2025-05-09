namespace MarketingAnalytics.Models
{
    public class UserActivity
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int ProductCategoryId { get; set; }
        public DateTime TimeStamp { get; set; }
        public InteractionType InteractionType { get; set; }
    }
}