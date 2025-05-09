namespace MarketingAnalytics.Models
{
    public class UserWeights
    {
        public int Id { get; set; }
        public required string UserId { get; set; }
        public required double[] Weights { get; set; }
    }
}