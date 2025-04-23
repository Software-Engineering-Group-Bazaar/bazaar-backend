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
    }
}