// --- DTOs/UserDtos.cs ---
using System;
using System.Collections.Generic; // For Roles list potentially
using System.ComponentModel.DataAnnotations;

namespace AdminApi.DTOs
{
    // DTO for creating a user (input)
    public class CreateUserDto
    {
        [Required]
        [MaxLength(256)]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)] // Should match Identity options
        public string Password { get; set; } = string.Empty;
    }

    // DTO for approving a user (input)
    public class ApproveUserDto
    {
        [Required]
        public string UserId { get; set; } = string.Empty;
    }

    // DTO for displaying user info (output)
    public class UserInfoDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool EmailConfirmed { get; set; }
        public IList<string> Roles { get; set; } = new List<string>();
        public bool IsApproved { get; set; }
        public bool IsActive { get; set; }
    }
}