namespace Users.Models.Dtos
{
    public class AddressDto
    {
        public int Id { get; set; }
        public string Address { get; set; } = string.Empty;
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string UserId { get; set; } = string.Empty;
    }
}