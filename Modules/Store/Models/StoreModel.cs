// Store/Models/StoreModel.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
// Dodaj using za User model ako je u drugom namespace-u
// using Users.Models;

namespace Store.Models
{
    public class StoreModel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; } // PascalCase

        [Required]
        [MaxLength(255)]
        public required string Name { get; set; } // PascalCase

        [Required]
        public int StoreCategoryId { get; set; } // Foreign Key ID

        [ForeignKey("StoreCategoryId")]
        public virtual StoreCategory StoreCategory { get; set; } = null!; // PascalCase navigacija

        [Required]
        public bool IsActive { get; set; } = true; // PascalCase

        [Required(ErrorMessage = "Ulica i broj su obavezni.")]
        [MaxLength(300)] // Prilagodi dužinu ako treba
        public required string StreetAndNumber { get; set; } // Novo polje

        [Required(ErrorMessage = "Grad/Mjesto je obavezno.")]
        [MaxLength(100)]
        public required string City { get; set; } // Novo polje

        [Required(ErrorMessage = "Općina je obavezna.")]
        [MaxLength(100)]
        public required string Municipality { get; set; } // Novo polje

        [MaxLength(20)] // Prilagodi dužinu
        public string? PostalCode { get; set; } // Opciono

        [MaxLength(100)] // Prilagodi dužinu
        public string? Country { get; set; } // Opciono
        // -------------------------


        public string? Description { get; set; } // PascalCase
    }
}