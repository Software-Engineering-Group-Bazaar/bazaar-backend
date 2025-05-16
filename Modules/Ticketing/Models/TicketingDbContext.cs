using Microsoft.EntityFrameworkCore;
using Ticketing.Models; // Referenca na tvoj Ticket model

namespace Ticketing.Data // Prilagodi namespace
{
    public class TicketingDbContext : DbContext
    {
        public DbSet<Ticket> Tickets { get; set; }

        public TicketingDbContext(DbContextOptions<TicketingDbContext> options)
            : base(options)
        {
        }
    }
}