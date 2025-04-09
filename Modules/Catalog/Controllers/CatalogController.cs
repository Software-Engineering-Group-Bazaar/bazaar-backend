using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Catalog.Models;
using Catalog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Controllers
{
    // [Authorize] // Sve akcije zahtijevaju autorizaciju
    [ApiController]
    [Route("api/[controller]")] // Bazna putanja: /api/catalog
    public class CatalogController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly IProductCategoryService _categoryService;

        // Konstruktor sada prima samo servise
        public CatalogController(
            IProductService productService,
            IProductCategoryService categoryService)
        {
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));
        }

        // --- Akcije za Kategorije (ProductCategory) ---

        [HttpGet("categories")] // GET /api/catalog/categories
        [ProducesResponseType(typeof(IEnumerable<ProductCategory>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllCategories()
        {
            var categories = await _categoryService.GetAllCategoriesAsync();
            return Ok(categories);
        }

        [HttpGet("categories/{id}")] // GET /api/catalog/categories/5
        [ProducesResponseType(typeof(ProductCategory), StatusCodes.Status200OK)]
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
            return Ok(category);
        }

        [HttpPost("categories")] // POST /api/catalog/categories
        [ProducesResponseType(typeof(ProductCategory), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)] // Dodan za slučaj duplikata
        public async Task<IActionResult> CreateCategory([FromBody] ProductCategory category)
        {
            if (category == null) return BadRequest("Category data is required.");
            // Osnovna validacija se očekuje od [ApiController] atributa

            try
            {
                // Servis bi trebao rukovati generiranjem ID-a
                if (category.Id != 0) category.Id = 0; // Osiguraj da se ne šalje ID

                var createdCategory = await _categoryService.CreateCategoryAsync(category);
                return CreatedAtAction(nameof(GetCategoryById), new { id = createdCategory.Id }, createdCategory);
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
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] ProductCategory category)
        {
            if (id <= 0 || category == null || id != category.Id)
            {
                return BadRequest("Route ID mismatch with body ID or invalid ID provided.");
            }

            try
            {
                var success = await _categoryService.UpdateCategoryAsync(category);

                if (!success)
                {
                    return NotFound(); // Kategorija nije pronađena
                }

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
        [ProducesResponseType(typeof(IEnumerable<Product>), StatusCodes.Status200OK)]
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

            return Ok(products);
        }

        [HttpGet("products/{id}")] // GET /api/catalog/products/15
        [ProducesResponseType(typeof(Product), StatusCodes.Status200OK)]
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
            return Ok(product);
        }

        [HttpPost("products")] // POST /api/catalog/products
        [ProducesResponseType(typeof(Product), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateProduct([FromBody] Product product)
        {
            if (product == null) return BadRequest("Product data is required.");
            // Osnovna validacija modela od [ApiController]

            try
            {
                if (product.Id != 0) product.Id = 0; // Osiguraj da baza generira ID

                // Dodatna validacija prije poziva servisa (npr. provjera categoryId)
                if (product.ProductCategory.Id <= 0) // Provjera eksplicitnog FK
                {
                    return BadRequest("Valid ProductCategoryId is required.");
                }
                // Opcionalno: Provjeriti postoji li kategorija ako servis to ne radi
                // var categoryExists = await _categoryService.GetCategoryByIdAsync(product.ProductCategoryId);
                // if (categoryExists == null) return BadRequest($"Category with ID {product.ProductCategoryId} not found.");


                var createdProduct = await _productService.CreateProductAsync(product);
                return CreatedAtAction(nameof(GetProductById), new { id = createdProduct.Id }, createdProduct);
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
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] Product product)
        {
            if (id <= 0 || product == null || id != product.Id)
            {
                return BadRequest("Route ID mismatch with body ID or invalid ID provided.");
            }

            try
            {
                // Dodatna validacija prije poziva servisa
                if (product.ProductCategory.Id <= 0)
                {
                    return BadRequest("Valid ProductCategoryId is required.");
                }
                // Opcionalno: Provjeriti postoji li kategorija ako servis to ne radi
                // var categoryExists = await _categoryService.GetCategoryByIdAsync(product.ProductCategoryId);
                // if (categoryExists == null) return BadRequest($"Category with ID {product.ProductCategoryId} not found.");

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
    }
}