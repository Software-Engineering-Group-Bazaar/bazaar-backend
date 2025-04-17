using System.ComponentModel.DataAnnotations;
namespace Store.Models
{
    public class StoreCreateDto
    {
        [Required(ErrorMessage = "Ime prodavnice je obavezno.")]
        [MaxLength(255)]
        public required string Name { get; set; }

        [Required(ErrorMessage = "ID kategorije prodavnice je obavezan.")]
        public int CategoryId { get; set; }

        // --- NOVA ADRESA ---
        [Required(ErrorMessage = "Ulica i broj su obavezni.")]
        [MaxLength(300)]
        public required string StreetAndNumber { get; set; }

        [Required(ErrorMessage = "Grad/Mjesto je obavezno.")]
        [MaxLength(100)]
        public required string City { get; set; }

        [Required(ErrorMessage = "Općina je obavezna.")]
        [MaxLength(100)]
        public required string Municipality { get; set; }

        [MaxLength(20)]
        public string? PostalCode { get; set; } // Opciono

        [MaxLength(100)]
        public string? Country { get; set; } // Opciono
                                             // --------------------

        public string? Description { get; set; } // Opciono

    }
}