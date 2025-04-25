namespace Review.Models.DTOs
{
    public class ReviewDto // Prikaz recenzije korisniku
    {
        public int Id { get; set; }
        public required string BuyerUsername { get; set; } // Mapirano iz BuyerId
        public int StoreId { get; set; }
        public int OrderId { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
        public DateTime DateTime { get; set; }
        public bool IsApproved { get; set; }
    }
}