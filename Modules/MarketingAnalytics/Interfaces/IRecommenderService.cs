using MarketingAnalytics.Models;

namespace MarketingAnalytics.Interfaces
{
    public interface IRecommenderService
    {
        Task<IEnumerable<Advertisment>> Recommend(string userId, int N = 1);
        Task<double> Score(Advertisment ad);
    }
}