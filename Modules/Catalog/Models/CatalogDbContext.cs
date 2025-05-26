using Catalog.Models;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Models
{
    public class CatalogDbContext : DbContext
    {

        public CatalogDbContext(DbContextOptions<CatalogDbContext> options)
        : base(options)
        {
        }

        // DbSet properties for each entity
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductCategory> ProductCategories { get; set; }
        public DbSet<ProductPicture> ProductPictures { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>()
                .Property(b => b.PointRate)
                .HasDefaultValue(1.0);
        }
    }
}