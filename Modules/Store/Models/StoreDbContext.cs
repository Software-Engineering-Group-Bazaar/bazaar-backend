using Microsoft.EntityFrameworkCore;
using Store.Models;

namespace Store.Models
{
    public class StoreDbContext : DbContext
    {
        // DbSet properties for each entity
        public DbSet<StoreModel> Stores { get; set; }
        public DbSet<StoreCategory> StoreCategories { get; set; }

        public StoreDbContext(DbContextOptions<StoreDbContext> options)
            : base(options)
        {
        }
    }
}