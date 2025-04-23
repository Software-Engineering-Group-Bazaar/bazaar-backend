using Microsoft.EntityFrameworkCore;

namespace Inventory.Models
{
    public class InventoryDbContext : DbContext
    {
        public DbSet<Inventory> Inventories { get; set; }

        public InventoryDbContext(DbContextOptions<InventoryDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Inventory>(entity =>
            {
            });
        }
    }
}