using System.ComponentModel.DataAnnotations;
namespace Store.Models
{
    public class StoreUpdateDto
    {
        [Required(ErrorMessage = "Ime prodavnice je obavezno.")]
        [MaxLength(255)]
        public required string Name { get; set; }

        [Required(ErrorMessage = "ID kategorije prodavnice je obavezan.")]
        public int CategoryId { get; set; }

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
        public string? PostalCode { get; set; }

        [MaxLength(100)]
        public string? Country { get; set; }

        public string? Description { get; set; }

        public bool IsActive { get; set; }
    }
}