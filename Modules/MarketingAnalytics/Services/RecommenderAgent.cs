using MarketingAnalytics.Interfaces;
using MarketingAnalytics.Models;

namespace MarketingAnalytics.Services
{
    public class RecommenderAgent : IRecommenderAgent
    {
        private readonly double learingRate;
        private readonly double exploreThreshold;
        public static readonly int featureDimension = 9;
        private readonly Random random = new Random();
        private readonly object _lock = new object();
        private double[] weights;
        public RecommenderAgent(double learingRate = 0.01, double exploreThreshold = 0.1)
        {
            this.learingRate = learingRate;
            this.exploreThreshold = exploreThreshold;
            weights = new double[featureDimension];
            for (int i = 0; i < featureDimension; i++)
                weights[i] = (random.NextDouble() - 0.5) * 1e-1;
        }
        public (Advertisment Ad, double[] FeatureVec) Recommend(string userId, List<Advertisment> candidates)
        {
            if (!candidates.Any())
                throw new InvalidDataException("No candidates provided");
            if (random.NextDouble() < exploreThreshold)
            {
                var randAd = candidates[random.Next(candidates.Count)];
                var features = FeatureEmbedding(userId, randAd);
                return (randAd, features);
            }

            throw new NotImplementedException();
        }
        public double[] FeatureEmbedding(string userId, Advertisment ad)
        {
            var f = new double[featureDimension];
            f[0] = 1;

            return new double[1];
        }
        public double Score(double[] featureVec)
        {
            double score = weights.Zip(featureVec, (x, y) => x * y).Sum();
            return score;
        }

        public void RecordReward()
        {
            throw new NotImplementedException();
        }
    }
}