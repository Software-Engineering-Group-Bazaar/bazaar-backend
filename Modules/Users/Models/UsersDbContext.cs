using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Users.Models
{
    public class UsersDbContext : IdentityDbContext<User>
    {
        public DbSet<PasswordResetRequest> PasswordResetRequests { get; set; }

        public UsersDbContext(DbContextOptions<UsersDbContext> options) : base(options)
        {

        }
    }
}