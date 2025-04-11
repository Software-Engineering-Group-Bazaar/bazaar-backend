
namespace Catalog.Dtos
{


    public class ProductDto
    {
        public string Name { get; set; } = string.Empty;
        public required int ProductCategoryId { get; set; }
        public decimal RetailPrice { get; set; }
        public decimal WholesalePrice { get; set; }
        public decimal? Weight { get; set; }
        public string? WeightUnit { get; set; }
        public decimal? Volume { get; set; }
        public string? VolumeUnit { get; set; }
        public required int StoreId { get; set; }
        public List<IFormFile>? Files { get; set; }
    }

    public class ProductGetDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ProductCategoryGetDto ProductCategory { get; set; } = new ProductCategoryGetDto { Id = 0, Name = "undefined" };
        public decimal RetailPrice { get; set; }
        public decimal WholesalePrice { get; set; }
        public decimal? Weight { get; set; }
        public string? WeightUnit { get; set; }
        public decimal? Volume { get; set; }
        public string? VolumeUnit { get; set; }
        public int StoreId { get; set; }

        // slike jos
    }

    public class ProductCategoryDto
    {
        public string Name { get; set; } = string.Empty;
    }
    public class ProductCategoryGetDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}