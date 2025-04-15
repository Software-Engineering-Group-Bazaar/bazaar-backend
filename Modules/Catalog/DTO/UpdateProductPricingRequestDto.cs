namespace Catalog.DTO
{
    public class UpdateProductPricingRequestDto
    {
        public int ProductId { get; set; }
        public decimal? RetailPrice { get; set; }
        public int? WholesaleThreshold { get; set; }
        public decimal? WholesalePrice { get; set; }
    }
}
