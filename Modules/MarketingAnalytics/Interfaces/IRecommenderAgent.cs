using MarketingAnalytics.Models;
using MarketingAnalytics.Services.DTOs;

namespace MarketingAnalytics.Interfaces
{
    public interface IRecommenderAgent
    {
        Task<List<AdFeaturePair>> RecommendAsync(string userId);
        Task<List<AdFeaturePair>> RecommendCandidatesAsync(string userId, List<Advertisment> candidates, int N = 1);
        Task<List<double[]>> FeatureEmbeddingListAsync(string userId, List<Advertisment> ads);
        Task<List<Func<double, double>>> GetTransformFuncsAsync(string userId);
        Task<double[]> FeatureEmbedding(string userId, Advertisment ad);
        Task<double> ScoreAd(Advertisment ad, string userId);
        Task<double> Score(double[] featureVec, string userId);
        double Score(double[] featureVec, double[] weights);
        Task<double[]> GetWeights(string userId);
        Task SetWeights(string userId, double[] weights);
        Task RecordRewardAsync(double[] featureVec, double reward, string userId);


    }
}