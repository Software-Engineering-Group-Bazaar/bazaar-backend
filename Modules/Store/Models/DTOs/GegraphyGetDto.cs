namespace Store.Models.Dtos
{
    public class GeographyGetDto
    {
        public List<RegionDto> Regions { get; set; } = new List<RegionDto>();
        public List<PlaceDto> Places { get; set; } = new List<PlaceDto>();
    }
    public class RegionDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string CountryCode { get; set; }
    }
    public class PlaceDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string PostalCode { get; set; }
    }
}