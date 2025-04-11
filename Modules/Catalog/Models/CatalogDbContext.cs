using Catalog.Models;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Models
{
    public class CatalogDbContext : DbContext
    {
        // DbSet properties for each entity
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductCategory> ProductCategories { get; set; }

        public CatalogDbContext(DbContextOptions<CatalogDbContext> options)
            : base(options)
        {
        }
    }
}