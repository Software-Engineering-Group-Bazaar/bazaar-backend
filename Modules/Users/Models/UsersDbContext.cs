using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Users.Models
{
    public class UsersDbContext : IdentityDbContext<User>
    {
        public UsersDbContext(DbContextOptions<UsersDbContext> options) : base(options)
        {

        }
    }
}