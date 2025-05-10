namespace MarketingAnalytics.DTOs
{
    public class AdvertismentFeatureDto
    {
        public AdvertismentDto Advertisment { get; set; }
        public double[] FeatureVec { get; set; }
    }
}