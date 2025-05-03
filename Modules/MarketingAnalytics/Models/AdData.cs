namespace MarketingAnalytics.Models
{
    public class AdData
    {
        public int Id { get; set; }
        public string? ImageUrl { get; set; }
        public int? StoreId { get; set; }
        public int? ProductId { get; set; }

        public int AdvertismentId { get; set; } // Required foreign key property
        public Advertisment Advertisment { get; set; } = null!;

    }
}