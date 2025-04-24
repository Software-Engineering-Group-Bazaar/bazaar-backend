using Review.Models;
using Review.Models.DTOs;

namespace Review.Interfaces
{
    public interface IReviewService
    {
        Task<IEnumerable<ReviewModel>> GetAllStoreReviewsAsync(int storeId);
        Task<ReviewModel?> GetOrderReviewAsync(int orderId);
        Task<double?> GetStoreAverageRatingAsync(int storeId);
        Task<ReviewModel?> CreateReviewAsync(ReviewModel review);
        Task<ReviewResponse?> CreateReviewResponseAsync(ReviewResponse response);
        Task<ReviewModel?> GetReviewByIdAsync(int reviewId);
    }
}