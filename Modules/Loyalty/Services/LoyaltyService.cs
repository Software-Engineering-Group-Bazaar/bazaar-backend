using Loyalty.Interfaces;
using Loyalty.Models;
using Microsoft.EntityFrameworkCore;
using Users.Interface;

namespace Loyalty.Services
{
    public class LoyaltyService : ILoyaltyService
    {
        private readonly IConfiguration _configuration;
        private readonly LoyaltyDbContext _context;
        private readonly IUserService _userService;
        public LoyaltyService(
            IConfiguration configuration,
            LoyaltyDbContext context,
            IUserService userService
        )
        {
            _configuration = configuration;
            _context = context;
            _userService = userService;
        }
        public async Task<int> GetUserPointsAsync(string userId)
        {
            var wallet = await _context.Wallets.Where(w => w.UserId == userId).FirstOrDefaultAsync();

            if (wallet == null)
            {
                await CreateWalletForUserAsync(userId);
                // create wallet
                return 0;
            }
            return wallet.Points;
        }
        public double GetAdminPaysSellerConst()
        {
            return LoyaltyRates.AdminPaysSeller;
        }
        public double GetSellerPaysAdminConst()
        {
            return LoyaltyRates.SellerPaysAdmin;
        }
        public double GetSpendingPointRateConst()
        {
            return LoyaltyRates.SpendingPointRate;
        }

        public async Task<Wallet> CreateWalletForUserAsync(string userId)
        {
            var user = await _userService.GetUsernameByIdAsync(userId);
            if (user == null)
            {
                throw new ArgumentException($"No user with userId: {userId}");
            }

            var wallet = await _context.Wallets.Where(w => w.UserId == userId).FirstOrDefaultAsync();

            if (wallet != null)
            {
                return wallet;
            }

            wallet = new Wallet
            {
                UserId = userId,
                Points = 0
            };

            try
            {
                await _context.Wallets.AddAsync(wallet);
                await _context.SaveChangesAsync();
                return wallet;
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException($"Error saving new wallet for userId: {userId}", ex);
            }
        }

        public async Task SetWalletPointsForUserAsync(string userId, int points)
        {
            var wallet = await _context.Wallets.Where(w => w.UserId == userId).FirstOrDefaultAsync();

            if (wallet == null)
            {
                await CreateWalletForUserAsync(userId);
                // create wallet
                throw new ArgumentException($"No wallet for userId {userId}");
            }

            if (points < 0)
            {
                throw new ArgumentException("Points can't be negative");
            }

            try
            {
                wallet.Points = points;
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException($"Error saving new wallet for userId: {userId}", ex);
            }
        }

        public async Task<double> GetAdminIncomeAsync(DateTime? from = null, DateTime? to = null, List<int>? storeIds = null)
        {
            var transactions = new List<Transaction>();
            await _context.Transactions.Where(t => (from == null || t.Timestamp >= from) && (to == null || t.Timestamp <= to) && (storeIds == null || storeIds.Contains(t.StoreId))).ToListAsync();

            var sellerPaysAdmin = GetSellerPaysAdminConst();

            return transactions.Sum(t => t.PointsQuantity * sellerPaysAdmin);
        }

        public async Task<double> GetAdminProfitAsync(DateTime? from = null, DateTime? to = null, List<int>? storeIds = null)
        {
            var transactions = new List<Transaction>();
            await _context.Transactions.Where(t => (from == null || t.Timestamp >= from) && (to == null || t.Timestamp <= to) && (storeIds == null || storeIds.Contains(t.StoreId))).ToListAsync();

            var sellerPaysAdmin = GetSellerPaysAdminConst();
            var adminPaysSeller = GetAdminPaysSellerConst();
            var profitConst = sellerPaysAdmin - adminPaysSeller;

            return transactions.Sum(t => t.PointsQuantity * profitConst);
        }

        public async Task<double> GetStoreIncomeAsync(int storeId, DateTime? from = null, DateTime? to = null)
        {
            var transactions = new List<Transaction>();
            await _context.Transactions.Where(t => t.StoreId == storeId && (from == null || t.Timestamp >= from) && (to == null || t.Timestamp <= to)).ToListAsync();

            var adminPaysSeller = GetAdminPaysSellerConst();

            return transactions.Sum(t => t.PointsQuantity * adminPaysSeller);
        }

        public async Task<Transaction> CreateTransaction(int orderId, string userId, int storeId, TransactionType transactionType, int points)
        {
            var userPoints = await GetUserPointsAsync(userId);
            if (transactionType == TransactionType.Buy)
            {
                var transaction = new Transaction
                {
                    Timestamp = DateTime.UtcNow,
                    OrderId = orderId,
                    PointsQuantity = points,
                    UserId = userId,
                    StoreId = storeId,
                    TransactionType = transactionType
                };

                try
                {
                    await _context.Transactions.AddAsync(transaction);
                    await _context.SaveChangesAsync();
                    await SetWalletPointsForUserAsync(userId, userPoints + points);
                    return transaction;
                }
                catch (DbUpdateException ex)
                {
                    throw new InvalidOperationException($"Error saving new transaction for orderId: {orderId}", ex);
                }
            }

            if (points > userPoints)
            {
                throw new ArgumentException($"No enough points");
            }

            var transaction1 = new Transaction
            {
                Timestamp = DateTime.UtcNow,
                OrderId = orderId,
                PointsQuantity = points,
                UserId = userId,
                StoreId = storeId,
                TransactionType = transactionType
            };

            try
            {
                await _context.Transactions.AddAsync(transaction1);
                await _context.SaveChangesAsync();
                await SetWalletPointsForUserAsync(userId, Math.Max(0, userPoints - points));
                return transaction1;
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException($"Error saving new transaction for orderId: {orderId}", ex);
            }
        }

        public static int PointsForProduct(decimal price, int quantity, double pointRate)
        {
            return (int)Math.Floor((double)price * quantity * pointRate);
        }
    }
}