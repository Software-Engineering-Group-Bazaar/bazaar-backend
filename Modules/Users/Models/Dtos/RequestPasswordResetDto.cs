using System.ComponentModel.DataAnnotations;

namespace Users.Models.Dtos
{
    public class RequestPasswordResetDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;
    }
}