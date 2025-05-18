namespace Users.Models
{
    public class Address
    {
        public int Id { get; set; }
        public string StreetAddress { get; set; } = string.Empty;
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string UserId { get; set; } = string.Empty;
        public User User { get; set; } = null!;
    }
}