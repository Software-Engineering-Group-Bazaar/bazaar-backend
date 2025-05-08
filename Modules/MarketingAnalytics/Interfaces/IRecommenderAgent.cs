using MarketingAnalytics.Models;

namespace MarketingAnalytics.Interfaces
{
    public interface IRecommenderAgent
    {
        (Advertisment Ad, double[] FeatureVec) RecommendAsync(string userId, List<Advertisment> candidates);
        void RecordReward();
    }
}