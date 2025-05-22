using Microsoft.EntityFrameworkCore;

namespace Loyalty.Models
{
    public class LoyaltyDbContext : DbContext
    {
        public DbSet<Wallet> Wallets { get; set; }
        public DbSet<Transaction> Transactions { get; set; }

        public LoyaltyDbContext(DbContextOptions<LoyaltyDbContext> options)
            : base(options)
        {
        }
    }
}