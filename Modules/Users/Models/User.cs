using Microsoft.AspNetCore.Identity;

namespace Users.Models
{
    public class User : IdentityUser
    {
        public bool IsApproved { get; set; }
    }
}