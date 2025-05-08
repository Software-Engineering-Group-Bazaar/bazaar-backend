using System.Text.Json;
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
        public DbSet<UserWeights> UserWeights { get; set; }
        public AdDbContext(DbContextOptions<AdDbContext> options)
        : base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<UserWeights>()
                    .Property(e => e.Weights)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                        v => JsonSerializer.Deserialize<double[]>(v, (JsonSerializerOptions)null)
                    );

        }
    }
}