using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Users.Models
{
    public class User : IdentityUser
    {
        public bool IsApproved { get; set; }
        public bool IsActive { get; set; }
        public int? StoreId { get; set; }
        public DateTime CreatedAt { get; set; }

        [MaxLength(500)]
        public string? FcmDeviceToken { get; set; }
        public ICollection<PasswordResetRequest> Posts { get; } = new List<PasswordResetRequest>();
    }
}