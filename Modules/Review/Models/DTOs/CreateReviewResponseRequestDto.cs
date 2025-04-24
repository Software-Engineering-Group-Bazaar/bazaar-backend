using System.ComponentModel.DataAnnotations;

namespace Review.Models.DTOs
{
    public class CreateReviewResponseRequestDto
    {
        [Required]
        public int ReviewId { get; set; }

        [Required]
        [StringLength(500, MinimumLength = 3, ErrorMessage = "Response must be between 3 and 500 characters.")]
        public required string Response { get; set; }
    }
}