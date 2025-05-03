namespace AdminApi.DTOs
{
    public class StoreDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public string CategoryName { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string PlaceName { get; set; } = "";
        public string RegionName { get; set; } = "";
    }
}