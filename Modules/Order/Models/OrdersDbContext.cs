using Microsoft.EntityFrameworkCore;

namespace Order.Models
{
    public class OrdersDbContext : DbContext
    {
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<OrderModel> Orders { get; set; }

        public OrdersDbContext(DbContextOptions<OrdersDbContext> options)
        : base(options)
        {
        }
    }
}