using Loyalty.Models;

namespace Loyalty.Interfaces
{
    public interface ILoyaltyService
    {
        Task<int> GetUserPointsAsync(string userId);
        double GetAdminPaysSellerConst();
        double GetSellerPaysAdminConst();
        double GetSpendingPointRateConst();
        Task<Wallet> CreateWalletForUserAsync(string userId);
        Task SetWalletPointsForUserAsync(string userId, int points);

        Task<double> GetAdminIncomeAsync(DateTime? from = null, DateTime? to = null, List<int>? storeIds = null);
        Task<double> GetAdminProfitAsync(DateTime? from = null, DateTime? to = null, List<int>? storeIds = null);
        Task<double> GetStoreIncomeAsync(int storeId, DateTime? from = null, DateTime? to = null);
        Task<Transaction> CreateTransaction(int orderId, string userId, int storeId, TransactionType transactionType, int points);
        static abstract int PointsForProduct(decimal price, int quantity, double pointRate);
    }
}