namespace Modules.Store.Models
{
    public class Product
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int ProductCategoryId { get; set; }

        public int StoreId { get; set; }

        public decimal Price { get; set; }

        public double? Weight { get; set; }

        public string? WeightUnit { get; set; }

        public double? Volume { get; set; }

        public string? VolumeUnit { get; set; }

        public List<ProductImage> ProductImages { get; set; } = new();
    }
}
