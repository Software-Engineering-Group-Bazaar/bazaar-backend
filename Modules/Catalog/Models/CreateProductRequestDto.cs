namespace Modules.Catalog.Models
{
    public class CreateProductRequestDto
    {
        public string Name { get; set; }
        public int ProductCategoryId { get; set; }
        public decimal Price { get; set; }

        public double? Weight { get; set; }
        public string? WeightUnit { get; set; }

        public double? Volume { get; set; }
        public string? VolumeUnit { get; set; }
    }
}
