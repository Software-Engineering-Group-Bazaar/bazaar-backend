namespace Review.Models.DTOs
{
    public class ReviewResponseDto // Prikaz odgovora korisniku
    {
        public int Id { get; set; }
        public int ReviewId { get; set; }
        public required string Response { get; set; }
        public DateTime DateTime { get; set; }
    }
}