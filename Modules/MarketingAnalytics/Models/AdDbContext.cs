using Microsoft.EntityFrameworkCore;

namespace MarketingAnalytics.Models
{
    public class AdDbContext : DbContext
    {
        public DbSet<AdData> AdData { get; set; }
        public DbSet<Advertisment> Advertisments { get; set; }

        public AdDbContext(DbContextOptions<AdDbContext> options)
        : base(options)
        {
        }

    }
}