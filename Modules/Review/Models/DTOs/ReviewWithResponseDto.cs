using Review.Models;
namespace Review.Models.DTOs
{
    public class ReviewWithResponseDto
    {

        public required ReviewDto Review { get; set; }

        public ReviewResponseDto? Response { get; set; }

    }
}