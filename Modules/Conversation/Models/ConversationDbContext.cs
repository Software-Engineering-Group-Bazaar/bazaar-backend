using Conversation.Models;
using Microsoft.EntityFrameworkCore;

namespace Conversation.Data
{
    public class ConversationDbContext : DbContext
    {
        public DbSet<Conversation.Models.Conversation> Conversations { get; set; }
        public DbSet<Message> Messages { get; set; }

        public ConversationDbContext(DbContextOptions<ConversationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Conversation.Models.Conversation> Reviews { get; set; }
        public DbSet<Message> ReviewResponses { get; set; }
    }
}