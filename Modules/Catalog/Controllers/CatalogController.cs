using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Catalog.DTO;
using Catalog.Dtos;
using Catalog.Models;
using Catalog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging; // Required for logging
using Store.Interface;

namespace Catalog.Controllers
{
    [Authorize(Roles = "Admin, Seller, Buyer")]
    [ApiController]
    [Route("api/[controller]")] // Bazna putanja: /api/catalog
    public class CatalogController : ControllerBase
    {

        private readonly IProductService _productService;
        private readonly IProductCategoryService _categoryService;

        private readonly IStoreService _storeService;

        // Konstruktor sada prima samo servise
        public CatalogController(
            IProductService productService,
            IProductCategoryService categoryService,
            IStoreService storeService)

        {
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));
            _storeService = storeService ?? throw new ArgumentNullException(nameof(storeService));
        }

        // --- Akcije za Kategorije (ProductCategory) ---

        [HttpGet("categories")] // GET /api/catalog/categories
        [ProducesResponseType(typeof(IEnumerable<ProductCategoryGetDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllCategories()
        {
            var categories = await _categoryService.GetAllCategoriesAsync();

            var categoryDtos = categories.Select(category => new ProductCategoryGetDto
            {
                Id = category.Id,
                Name = category.Name
            }).ToList();
            return Ok(categoryDtos);
        }

        [HttpGet("categories/{id}")] // GET /api/catalog/categories/5
        [ProducesResponseType(typeof(ProductCategoryGetDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetCategoryById(int id)
        {
            if (id <= 0) return BadRequest("Invalid category ID.");

            var category = await _categoryService.GetCategoryByIdAsync(id);
            if (category == null)
            {
                return NotFound();
            }
            var categoryDto = new ProductCategoryGetDto
            {
                Id = category.Id,
                Name = category.Name
            };
            return Ok(categoryDto);
        }

        [HttpPost("categories")] // POST /api/catalog/categories
        [ProducesResponseType(typeof(ProductCategoryGetDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)] // Dodan za slučaj duplikata
        public async Task<IActionResult> CreateCategory([FromBody] ProductCategoryDto category)
        {
            if (category == null) return BadRequest("Category data is required.");
            // Osnovna validacija se očekuje od [ApiController] atributa

            try
            {
                var productCategory = new ProductCategory
                {
                    Id = 0,
                    Name = category.Name
                };

                var createdCategory = await _categoryService.CreateCategoryAsync(productCategory);

                var createdCategoryDto = new ProductCategoryGetDto
                {
                    Id = createdCategory.Id,
                    Name = createdCategory.Name
                };

                return CreatedAtAction(nameof(GetAllCategories), new { }, createdCategoryDto);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex) // Npr. ako ime već postoji
            {
                return Conflict(ex.Message);
            }
            catch (Exception) // Općenita greška
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the category.");
            }
        }

        [HttpPut("categories/{id}")] // PUT /api/catalog/categories/5
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)] // Dodan za slučaj duplikata imena
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] ProductCategoryDto category)
        {
            if (id <= 0 || category == null)
            {
                return BadRequest("Route ID mismatch with body ID or invalid ID provided.");
            }

            try
            {
                var categoryOld = await _categoryService.GetCategoryByIdAsync(id);

                if (categoryOld == null)
                {
                    return NotFound();
                }

                categoryOld.Name = category.Name;

                var success = await _categoryService.UpdateCategoryAsync(categoryOld);

                return NoContent(); // Uspješno ažurirano
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex) // Npr. ako novo ime već postoji
            {
                return Conflict(ex.Message);
            }
            catch (Exception) // Općenita greška
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the category.");
            }
        }

        [HttpDelete("categories/{id}")] // DELETE /api/catalog/categories/5
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            if (id <= 0) return BadRequest("Invalid category ID.");

            try
            {
                var success = await _categoryService.DeleteCategoryAsync(id);

                if (!success)
                {
                    return NotFound();
                }

                return NoContent(); // Uspješno obrisano
            }
            catch (Exception) // Paziti na DbUpdateException (foreign key constraints)
            {
                // Ovisno o zahtjevima, možete vratiti BadRequest/Conflict ili 500
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the category. It might be in use.");
            }
        }


        // --- Akcije za Proizvode (Product) ---

        [HttpGet("products")] // GET /api/catalog/products?categoryId=2&storeId=10
        [ProducesResponseType(typeof(IEnumerable<ProductGetDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetProducts([FromQuery] int? categoryId, [FromQuery] int? storeId)
        {
            IEnumerable<Product> products;

            // Validacija ID-jeva
            if ((categoryId.HasValue && categoryId <= 0) || (storeId.HasValue && storeId <= 0))
            {
                return BadRequest("Invalid Category or Store ID provided.");
            }


            if (categoryId.HasValue && storeId.HasValue)
            {
                // Filtriranje po oba - Oprez: filtriranje u memoriji ako servis ne podržava oba filtera
                var byCategory = await _productService.GetProductsByCategoryIdAsync(categoryId.Value);
                products = byCategory.Where(p => p.StoreId == storeId.Value);
            }
            else if (categoryId.HasValue)
            {
                products = await _productService.GetProductsByCategoryIdAsync(categoryId.Value);
            }
            else if (storeId.HasValue)
            {
                products = await _productService.GetProductsByStoreIdAsync(storeId.Value);
            }
            else
            {
                products = await _productService.GetAllProductsAsync();
            }

            var productsDto = products.Select(product => new ProductGetDto
            {
                Id = product.Id,
                Name = product.Name,
                ProductCategory = new ProductCategoryGetDto { Id = product.ProductCategory.Id, Name = product.ProductCategory.Name },
                RetailPrice = product.RetailPrice,
                WholesaleThreshold = product.WholesaleThreshold,
                WholesalePrice = product.WholesalePrice,
                Weight = product.Weight,
                WeightUnit = product.WeightUnit,
                Volume = product.Volume,
                VolumeUnit = product.VolumeUnit,
                StoreId = product.StoreId,
                IsActive = product.IsActive,
                Photos = product.Pictures.Select(photo => photo.Url).ToList()
            }).ToList();

            return Ok(productsDto);
        }

        [HttpGet("products/{id}")] // GET /api/catalog/products/15
        [ProducesResponseType(typeof(ProductGetDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProductById(int id)
        {
            if (id <= 0) return BadRequest("Invalid product ID.");

            var product = await _productService.GetProductByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            var productDto = new ProductGetDto
            {
                Id = product.Id,
                Name = product.Name,
                ProductCategory = new ProductCategoryGetDto { Id = product.ProductCategory.Id, Name = product.ProductCategory.Name },
                RetailPrice = product.RetailPrice,
                WholesaleThreshold = product.WholesaleThreshold,
                WholesalePrice = product.WholesalePrice,
                Weight = product.Weight,
                WeightUnit = product.WeightUnit,
                Volume = product.Volume,
                VolumeUnit = product.VolumeUnit,
                StoreId = product.StoreId,
                IsActive = product.IsActive,
                Photos = product.Pictures.Select(photo => photo.Url).ToList()
            };

            return Ok(productDto);
        }

        // POST /api/admin/products/create
        [HttpPost("products/create")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)] // Updated success response type
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateProduct([FromForm] ProductDto createProductDto)
        {
            try
            {

                var store = _storeService.GetStoreById(createProductDto.StoreId);

                if (store is null)
                {
                    return BadRequest($"store with id:{createProductDto.StoreId} does not exist");
                }

                var product = new Product
                {
                    Id = 0,
                    Name = createProductDto.Name,
                    ProductCategoryId = createProductDto.ProductCategoryId,
                    ProductCategory = new ProductCategory
                    {
                        Id = createProductDto.ProductCategoryId,
                        Name = "nezz"
                    },
                    RetailPrice = createProductDto.RetailPrice,
                    WholesaleThreshold = createProductDto.WholesaleThreshold,
                    WholesalePrice = createProductDto.WholesalePrice,
                    Weight = createProductDto.Weight,
                    WeightUnit = createProductDto.WeightUnit,
                    Volume = createProductDto.Volume,
                    VolumeUnit = createProductDto.VolumeUnit,
                    StoreId = createProductDto.StoreId,
                    IsActive = createProductDto.IsActive
                };

                var createdProduct = await _productService.CreateProductAsync(product, createProductDto.Files);
                var createdProductDto = new ProductGetDto
                {
                    Id = product.Id,
                    Name = product.Name,
                    ProductCategory = new ProductCategoryGetDto { Id = product.ProductCategory.Id, Name = product.ProductCategory.Name },
                    RetailPrice = product.RetailPrice,
                    WholesaleThreshold = product.WholesaleThreshold,
                    WholesalePrice = product.WholesalePrice,
                    Weight = product.Weight,
                    WeightUnit = product.WeightUnit,
                    Volume = product.Volume,
                    VolumeUnit = product.VolumeUnit,
                    StoreId = product.StoreId,
                    IsActive = product.IsActive,
                    Photos = product.Pictures.Select(photo => photo.Url).ToList()
                };

                return CreatedAtAction(nameof(CreateProduct), new { }, createdProductDto);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex) // Npr. nepostojeća kategorija u servisu
            {
                // Može biti BadRequest ili NotFound ovisno o uzroku
                return BadRequest(ex.Message);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the product.");
            }


        }

        [HttpPut("products/{id}")] // PUT /api/catalog/products/15
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] ProductDto productDto)
        {
            if (id <= 0 || productDto == null)
            {
                return BadRequest("Route ID mismatch with body ID or invalid ID provided.");
            }

            try
            {
                // Dodatna validacija prije poziva servisa
                if (productDto.ProductCategoryId <= 0)
                {
                    return BadRequest("Valid ProductCategoryId is required.");
                }
                var category = await _categoryService.GetCategoryByIdAsync(productDto.ProductCategoryId);
                if (category == null) return BadRequest($"Category with ID {productDto.ProductCategoryId} not found.");

                var product = await _productService.GetProductByIdAsync(id);
                if (product == null) return BadRequest($"Product with ID {id} not found.");


                product.Name = productDto.Name;
                product.ProductCategory = category;
                product.RetailPrice = productDto.RetailPrice;
                product.WholesaleThreshold = productDto.WholesaleThreshold;
                product.WholesalePrice = productDto.WholesalePrice;
                product.Weight = productDto.Weight;
                product.WeightUnit = productDto.WeightUnit;
                product.Volume = productDto.Volume;
                product.VolumeUnit = productDto.VolumeUnit;
                product.StoreId = productDto.StoreId;
                product.IsActive = productDto.IsActive;

                var success = await _productService.UpdateProductAsync(product);
                if (!success)
                {
                    return NotFound();
                }
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex) // Npr. nepostojeća kategorija u servisu
            {
                return BadRequest(ex.Message);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the product.");
            }
        }

        [HttpDelete("products/{id}")] // DELETE /api/catalog/products/15
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            if (id <= 0) return BadRequest("Invalid product ID.");

            try
            {
                var success = await _productService.DeleteProductAsync(id);
                if (!success)
                {
                    return NotFound();
                }
                return NoContent();
            }
            catch (Exception) // Paziti na DbUpdateException (foreign key constraints)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the product. It might be in use.");
            }
        }

        [HttpGet("search")] // GET api/products/search
        [ProducesResponseType(typeof(IEnumerable<ProductGetDto>), 200)]
        [ProducesResponseType(400)]
        public async Task<ActionResult<IEnumerable<ProductGetDto>>> SearchProducts([FromQuery] string searchTerm = "")
        {

            if (searchTerm is null)
            {
                searchTerm = string.Empty;
            }

            var products = await _productService.SearchProductsByNameAsync(searchTerm);

            var productsDto = products.Select(product => new ProductGetDto
            {
                Id = product.Id,
                Name = product.Name,
                ProductCategory = new ProductCategoryGetDto { Id = product.ProductCategory.Id, Name = product.ProductCategory.Name },
                RetailPrice = product.RetailPrice,
                WholesaleThreshold = product.WholesaleThreshold,
                WholesalePrice = product.WholesalePrice,
                Weight = product.Weight,
                WeightUnit = product.WeightUnit,
                Volume = product.Volume,
                VolumeUnit = product.VolumeUnit,
                StoreId = product.StoreId,
                IsActive = product.IsActive,
                Photos = product.Pictures.Select(photo => photo.Url).ToList()
            }).ToList();

            return Ok(productsDto);
        }

        [HttpPut("products/{productId:int}/pricing")]
        [Authorize(Roles = "Seller,Admin")]
        [ProducesResponseType(typeof(ProductGetDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateProductPricing(int productId, [FromBody] UpdateProductPricingRequestDto pricingData)
        {
            // Osnovna validacija ID-ja iz rute
            if (productId <= 0)
            {
                return BadRequest("Invalid Product ID provided in URL.");
            }
            // Validacija DTO objekta (npr. [Range] atributi) će se izvršiti automatski
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Dobij ID korisnika koji šalje zahtjev
            var requestingUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(requestingUserId))
            {
                return Unauthorized("User identifier not found.");
            }

            try
            {
                var updatedProductDto = await _productService.UpdateProductPricingAsync(requestingUserId, productId, pricingData);

                if (updatedProductDto == null)
                {
                    return NotFound($"Product with ID {productId} not found.");
                }
                return Ok(updatedProductDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "User validation error during pricing update.");
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating product pricing.");
            }
        }

        // PUT /api/catalog/products/{productId}/availability
        [HttpPut("products/{productId:int}/availability")]
        [Authorize(Roles = "Seller,Admin")]
        [Produces("application/json")] // Tipovi odgovora u slučaju greške
        [ProducesResponseType(StatusCodes.Status204NoContent)] // Uspjeh
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateProductAvailability(int productId, [FromBody] UpdateProductAvailabilityRequestDto availabilityDto)
        {
            // Validacija ulaznih parametara
            if (productId <= 0)
            {
                ModelState.AddModelError(nameof(productId), "Invalid Product ID provided in URL.");
                return BadRequest(ValidationProblem(ModelState)); // Vrati ProblemDetails za konzistentnost
            }

            if (!ModelState.IsValid)
            {
                // ModelState već sadrži greške iz DTO validacije
                return BadRequest(ValidationProblem(ModelState));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                // Ovo je tehnički 401, ali pošto imamo [Authorize], rijetko se desi
                return Unauthorized("User identifier not found.");
            }
            try
            {
                // Pozovi servis
                var success = await _productService.UpdateProductAvailabilityAsync(userId, productId, availabilityDto.IsActive);

                if (!success)
                {
                    // Servis vraća false samo ako proizvod nije nađen
                    return NotFound($"Product with ID {productId} not found."); // 404 Not Found
                }

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                // Vrati ProblemDetails za 403 radi konzistentnosti sa ValidationProblem
                return Problem(detail: "User is not authorized to update availability for this product.", statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");
            }
            catch (ArgumentException ex)
            {
                // Vrati ProblemDetails
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Bad Request");
            }
            catch (KeyNotFoundException ex) // Ako servis ne nađe korisnika
            {
                // Vrati ProblemDetails za 500
                return Problem(detail: "User validation error during availability update.", statusCode: StatusCodes.Status500InternalServerError, title: "Internal Server Error");
            }
            catch (Exception ex) // Sve ostale greške
            {
                // Vrati ProblemDetails za 500
                return Problem(detail: "An error occurred while updating product availability.", statusCode: StatusCodes.Status500InternalServerError, title: "Internal Server Error");
            }
        }
    }
}


