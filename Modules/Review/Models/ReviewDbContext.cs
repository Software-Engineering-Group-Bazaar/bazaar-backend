using Microsoft.EntityFrameworkCore;
using Review.Models;

namespace Review.Models
{
    public class ReviewDbContext : DbContext
    {

        public ReviewDbContext(DbContextOptions<ReviewDbContext> options)
        : base(options)
        {
        }

        // DbSet properties for each entity
        public DbSet<ReviewModel> Reviews { get; set; }
        public DbSet<ReviewResponse> ReviewResponses { get; set; }
    }
}