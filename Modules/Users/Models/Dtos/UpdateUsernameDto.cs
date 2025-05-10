using System.ComponentModel.DataAnnotations;

public class UpdateUsernameDto
{
    [Required(ErrorMessage = "New username is required.")]
    [StringLength(256, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 256 characters.")]
    // Opciono: Dodaj RegularExpression ako imaš pravila za format username-a
    public required string NewUsername { get; set; }
}