namespace Modules.Catalog.Models
{
    public class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string CategoryName { get; set; }
        public decimal Price { get; set; }

        public List<string> ImageUrls { get; set; } = new();
    }
}
