using Microsoft.EntityFrameworkCore;

namespace Delivery.Models
{
    public class DeliveryDbContext : DbContext
    {
        public DeliveryDbContext(DbContextOptions<DeliveryDbContext> options) : base(options) { }

        public DbSet<DeliveryRoute> Routes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<DeliveryRoute>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.Property(r => r.OwnerId).IsRequired();

                // Configure OrderIds to be stored as an integer array in PostgreSQL
                // Npgsql provider handles List<int> to integer[] mapping automatically.
                // If you needed specific array type (e.g., text[]), you could specify:
                // entity.Property(r => r.OrderIds).HasColumnType("integer[]");

                // Configure RouteData as a JSON column
                // This tells EF Core to serialize the RouteDataPoco object into a JSON string
                // and store it in a 'jsonb' column (default for Npgsql when using .ToJson()).
                entity.OwnsOne(r => r.RouteData, ownedNavigationBuilder =>
                {
                    ownedNavigationBuilder.ToJson(); // This is the key for JSON column mapping!
                                                     // You can further configure properties within RouteDataPoco if needed
                                                     // e.g., ownedNavigationBuilder.Property(rd => rd.Data).IsRequired();
                                                     // ownedNavigationBuilder.Property(rd => rd.Hash).HasMaxLength(64);
                });

                entity.Property(r => r.CreatedAt).ValueGeneratedOnAdd().HasDefaultValueSql("CURRENT_TIMESTAMP");
                entity.Property(r => r.UpdatedAt).ValueGeneratedOnAddOrUpdate().HasDefaultValueSql("CURRENT_TIMESTAMP");
            });
        }
    }
}