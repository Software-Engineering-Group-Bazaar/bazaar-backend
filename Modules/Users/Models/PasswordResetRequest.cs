using System;
using System.ComponentModel.DataAnnotations;
using Users.Models;

namespace Users.Models
{
    public class PasswordResetRequest
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = null!; // Foreign key ka AspNetUsers tabeli (string ako koristite default Identity)
        public virtual User User { get; set; } = null!; // Navigation property

        [Required]
        public string HashedCode { get; set; } = null!; // Hashirani kod za reset

        [Required]
        public DateTime ExpiryDateTimeUtc { get; set; } // Vrijeme kada kod ističe (uvek koristi UTC)

        public DateTime CreatedDateTimeUtc { get; set; } = DateTime.UtcNow; // Vrijeme kreiranja

        public bool IsUsed { get; set; } = false; // Da li je kod iskorišten
    }
}