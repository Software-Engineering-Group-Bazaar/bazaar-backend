using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Catalog.Interfaces;
using Catalog.DTO;
using Catalog.Services;

namespace Catalog.Controllers
{
    [Route("api/catalog")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        [HttpPost("product/isAvailable")]
        [Authorize(Roles = "Seller")]
        public async Task<IActionResult> UpdateProductAvailability([FromBody] UpdateProductAvailabilityRequestDto request)
        {
            // Izvuci sellerUserId iz tokena
            var sellerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(sellerUserId))
            {
                return Unauthorized("Nema korisnika u tokenu.");
            }

            var result = await _productService.UpdateProductAvailabilityAsync(
                sellerUserId,
                request.ProductId,
                request.IsAvailable
            );

            if (!result)
            {
                return BadRequest("Nije moguće ažurirati dostupnost proizvoda.");
            }

            return Ok("Dostupnost proizvoda je uspješno ažurirana.");
        }
    }
}

