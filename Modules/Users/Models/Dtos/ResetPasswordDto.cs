using System.ComponentModel.DataAnnotations;

namespace Users.Models.Dtos // Ili gde držite DTOs
{
    public class ResetPasswordDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        // Možete dodati StringLength ili Regex ako želite specifičan format koda (npr. 6 cifara)
        [MinLength(6, ErrorMessage = "Kod mora imati bar 6 karaktera.")]
        public string Code { get; set; } = null!;

        [Required]
        [DataType(DataType.Password)]
        // Dodajte atribute za jačinu lozinke ako su definisani u Identity podešavanjima
        [MinLength(6, ErrorMessage = "Lozinka mora imati bar 6 karaktera.")]
        public string NewPassword { get; set; } = null!;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Lozinke se ne poklapaju.")]
        public string ConfirmPassword { get; set; } = null!;
    }
}