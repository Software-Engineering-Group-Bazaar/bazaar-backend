

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Modules.Catalog.Models;
using Modules.Catalog.Services; 
namespace Modules.Catalog.Controllers
{
    [ApiController]
    [Route("api/store/{storeId}/products")]
    [Authorize(Roles = "Seller")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        [HttpPost]
        public async Task<IActionResult> AddProductToStore(
            int storeId,
            [FromForm] CreateProductRequestDto productData,
            [FromForm] List<IFormFile> imageFiles)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (userId == null)
                return Unauthorized();

            var result = await _productService.AddProductToStoreAsync(userId, storeId, productData, imageFiles);

            if (result == null)
                return BadRequest("Proizvod se nije mogao kreirati.");

            return CreatedAtAction(nameof(AddProductToStore), new { id = result.Id }, result);
        }
    }
}
