
using System.ComponentModel.DataAnnotations;

namespace Catalog.Dtos
{


    public class ProductDto
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
        public List<IFormFile>? Files { get; set; }
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

        public List<string>? Photos { get; set; } = new List<string>();
        public double PointRate { get; set; }
    }

    public class UpdateProductPointRateRequest
    {
        [Required]
        [Range(0.0, double.MaxValue, ErrorMessage = "Point rate must be greater than or equal to 0.")]
        public double PointRate { get; set; }
    }

    public class UpdateProductAvailabilityRequestDto
    {

        [Required(ErrorMessage = "Availability status is required.")]
        public bool IsActive { get; set; }
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

    public class FilterBodyDto
    {
        public string query { get; set; } = string.Empty;
        public List<string> places { get; set; } = new List<string>();
        public string region { get; set; } = string.Empty;
        public string category { get; set; } = string.Empty;

    }

    public class ProductsByStoresGetDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<ProductGetDto> Products { get; set; } = new List<ProductGetDto>();
    }
}