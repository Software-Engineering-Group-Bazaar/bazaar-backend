namespace MarketingAnalytics.Models
{
    public class Advertisment
    {
        public int Id { get; set; }
        public string SellerId { get; set; } = string.Empty;
        public int Views { get; set; }
        public decimal ViewPrice { get; set; }
        public ICollection<Views> ViewTimestamps { get; } = new List<Views>();
        public int Clicks { get; set; }
        public decimal ClickPrice { get; set; }
        public ICollection<Clicks> ClickTimestamps { get; } = new List<Clicks>();
        public int Conversions { get; set; }
        public decimal ConversionPrice { get; set; }
        public ICollection<Conversions> ConversionTimestamps { get; } = new List<Conversions>();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsActive { get; set; }
        public ICollection<AdData> AdData { get; } = new List<AdData>();
        public AdType AdType { get; set; } = AdType.Fixed;
    }
}