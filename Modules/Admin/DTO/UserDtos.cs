// --- DTOs/UserDtos.cs ---
using System;
using System.Collections.Generic; // For Roles list potentially
using System.ComponentModel.DataAnnotations;

namespace AdminApi.DTOs
{
    // DTO for creating a user (input)

    public class UpdateUserDto
    {
        public string UserName { get; set; }
        public required string Id { get; set; }
        public string Role { get; set; }
        public bool IsActive { get; set; }
        public bool IsApproved { get; set; }
        public string Email { get; set; }

    }
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
        public string Role { get; set; } = "Seller";
    }

    // DTO for approving a user (input)
    public class ApproveUserDto
    {
        [Required]
        public string UserId { get; set; } = string.Empty;
    }
    public class ActivateUserDto
    {
        [Required]
        public string UserId { get; set; } = string.Empty;
        [Required]
        public bool ActivationStatus { get; set; }
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

        public static implicit operator UserInfoDto(UserInfoDto v)
        {
            throw new NotImplementedException();
        }
    }
}