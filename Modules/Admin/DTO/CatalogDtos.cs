namespace AdminApi.DTOs
{
    public class UpdateProductDto
    {
        public string Name { get; set; } = string.Empty;
        public required int ProductCategoryId { get; set; }
        public decimal RetailPrice { get; set; }
        public int? WholesaleThreshold { get; set; }
        public decimal WholesalePrice { get; set; }
        public decimal? Weight { get; set; }
        public string? WeightUnit { get; set; }
        public decimal? Volume { get; set; }
        public string? VolumeUnit { get; set; }
        public required int StoreId { get; set; }
        public bool IsActive { get; set; } = true;
        public List<string>? Files { get; set; }
    }
}