namespace MarketingAnalytics.Models
{
    public class Conversions
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public int AdvertismentId { get; set; }
        public Advertisment Advertisment { get; set; } = null!;
    }
}