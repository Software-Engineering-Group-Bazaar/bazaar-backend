using System.ComponentModel.DataAnnotations;

namespace Catalog.DTO
{
    public class UpdateProductPricingRequestDto
    {
        [Range(0.01, (double)decimal.MaxValue)]
        public decimal? RetailPrice { get; set; }

        [Range(0, int.MaxValue)]
        public int? WholesaleThreshold { get; set; }

        [Range(0.00, (double)decimal.MaxValue)]
        public decimal? WholesalePrice { get; set; }
    }
}
