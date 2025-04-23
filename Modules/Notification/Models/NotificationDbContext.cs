using Microsoft.EntityFrameworkCore;
using Notifications.Models;

namespace Notifications.Models
{
    public class NotificationsDbContext : DbContext
    {
        public DbSet<Notification> Notifications { get; set; }

        public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasIndex(e => new { e.UserId, e.IsRead, e.Timestamp })
                      .HasDatabaseName("IX_Notifications_User_Read_Time");

                entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();
                entity.Property(e => e.Message).HasMaxLength(500).IsRequired();
                entity.Property(e => e.OrderId).IsRequired(false);
            });
        }
    }
}