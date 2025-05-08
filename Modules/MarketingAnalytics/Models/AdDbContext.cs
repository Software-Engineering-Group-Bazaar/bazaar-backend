using Microsoft.EntityFrameworkCore;

namespace MarketingAnalytics.Models
{
    public class AdDbContext : DbContext
    {
        public DbSet<AdData> AdData { get; set; }
        public DbSet<Advertisment> Advertisments { get; set; }
        public DbSet<Clicks> Clicks { get; set; }
        public DbSet<Views> Views { get; set; }
        public DbSet<Conversions> Conversions { get; set; }
        public DbSet<UserActivity> UserActivities { get; set; }

        public AdDbContext(DbContextOptions<AdDbContext> options)
        : base(options)
        {
        }

    }
}