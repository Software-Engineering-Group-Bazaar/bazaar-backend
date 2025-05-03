namespace MarketingAnalytics.Models
{
    public class Advertisment
    {
        public int Id { get; set; }
        public string SellerId { get; set; }
        public int Views { get; set; }
        public int Clicks { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsActive { get; set; }

        public ICollection<AdData> AdData { get; } = new List<AdData>();

    }
}