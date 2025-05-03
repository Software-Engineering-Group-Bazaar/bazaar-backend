using Catalog.Dtos;

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
    public class ProductGetDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ProductCategoryGetDto ProductCategory { get; set; } = new ProductCategoryGetDto { Id = 0, Name = "undefined" };
        public decimal RetailPrice { get; set; }
        public int? WholesaleThreshold { get; set; }
        public decimal? WholesalePrice { get; set; }
        public decimal? Weight { get; set; }
        public string? WeightUnit { get; set; }
        public decimal? Volume { get; set; }
        public string? VolumeUnit { get; set; }

        public bool IsActive { get; set; } = true;
        public int StoreId { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string>? Photos { get; set; } = new List<string>();
    }
}