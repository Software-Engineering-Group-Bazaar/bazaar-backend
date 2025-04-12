using Microsoft.AspNetCore.Identity;

namespace Users.Models
{
    public class User : IdentityUser
    {
        public bool IsApproved { get; set; }
        public bool IsActive { get; set; }
        public int? StoreId { get; set; }
        public ICollection<PasswordResetRequest> Posts { get; } = new List<PasswordResetRequest>();
    }
}