using Microsoft.EntityFrameworkCore;
using Order.Models;

namespace Order.Models
{
    public class OrderDbContext : DbContext
    {
        public OrderDbContext(DbContextOptions<OrderDbContext> options)
            : base(options)
        {
        }

        public DbSet<OrderModel> Orders { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<OrderModel>(entity =>
            {
                entity.ToTable("Orders");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Status)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(e => e.CreatedAt)
                      .IsRequired();
            });
        }
    }
}
