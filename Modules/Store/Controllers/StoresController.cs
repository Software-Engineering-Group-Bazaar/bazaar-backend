using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdminApi.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Store.Interface;
using Store.Models;


namespace Store.Controllers
{
    [Authorize(Roles = "Admin, Seller, Buyer")]
    [ApiController]
    [Route("api/[controller]")] // Osnovna ruta će biti /api/Stores
    public class StoresController : ControllerBase
    {
        private readonly IStoreService _storeService;
        private readonly IStoreCategoryService _storeCategoryService;
        private readonly ILogger<StoresController> _logger;

        public StoresController(
            IStoreService storeService,
            IStoreCategoryService storeCategoryService,
            ILogger<StoresController> logger)
        {
            _storeService = storeService;
            _storeCategoryService = storeCategoryService;
            _logger = logger;
        }

        // GET /api/Stores
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<StoreGetDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)] // Dobra praksa je dodati i ovo
        [ProducesResponseType(StatusCodes.Status403Forbidden)]   // I ovo, ako korisnik nema traženu ulogu
        public ActionResult<IEnumerable<StoreGetDto>> GetStores()
        {

            _logger.LogInformation("[StoresController] Attempting to retrieve all stores.");

            try
            {
                var stores = _storeService.GetAllStores();
                if (stores == null || !stores.Any())
                {
                    _logger.LogInformation("[StoresController] No stores found.");
                    return Ok(new List<StoreGetDto>());
                }

                var storeDtos = stores.Select(store => new StoreGetDto
                {
                    Id = store.id,
                    Name = store.name,
                    Address = store.address,
                    Description = store.description,
                    IsActive = store.isActive,
                    CategoryName = store.category?.name ?? "N/A"
                }).ToList();

                _logger.LogInformation("[StoresController] Successfully retrieved {StoreCount} stores.", storeDtos.Count);
                return Ok(storeDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[StoresController] An error occurred while retrieving stores.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving stores.");
            }
        }

        // GET /api/Stores/Categories
        [HttpGet("Categories")]
        [ProducesResponseType(typeof(IEnumerable<StoreCategoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public ActionResult<IEnumerable<StoreCategoryDto>> GetStoreCategories()
        {
            _logger.LogInformation("[StoresController] Attempting to retrieve store categories.");

            try
            {
                var categories = _storeCategoryService.GetAllCategories();
                if (categories == null || !categories.Any())
                {
                    _logger.LogInformation("[StoresController] No store categories found.");
                    return Ok(new List<StoreCategoryDto>());
                }

                var categoryDtos = categories.Select(c => new StoreCategoryDto
                {
                    Id = c.id,
                    Name = c.name
                }).ToList();

                _logger.LogInformation("[StoresController] Successfully retrieved {CategoryCount} store categories.", categoryDtos.Count);
                return Ok(categoryDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[StoresController] An error occurred while retrieving store categories.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving store categories.");
            }
        }

        // POST /api/Stores
        [HttpPost]
        [ProducesResponseType(typeof(StoreGetDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public ActionResult<StoreGetDto> CreateStore([FromBody] StoreCreateDto dto)
        {
            _logger.LogInformation("[StoresController] Attempting to create a new store with Name: {StoreName}, CategoryId: {CategoryId}", dto.Name, dto.CategoryId);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("[StoresController] Create store request failed model validation. Errors: {@ModelState}", ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }

            try
            {
                var category = _storeCategoryService.GetCategoryById(dto.CategoryId);
                if (category == null)
                {
                    _logger.LogWarning("[StoresController] Category with ID {CategoryId} not found for creating store.", dto.CategoryId);
                    return BadRequest($"Category with ID {dto.CategoryId} does not exist.");
                }

                var store = _storeService.CreateStore(dto.Name, dto.CategoryId, dto.Address, dto.Description);

                var storeDto = new StoreGetDto
                {
                    Id = store.id,
                    Name = store.name,
                    Address = store.address,
                    Description = store.description,
                    IsActive = store.isActive,
                    CategoryName = category.name
                };

                _logger.LogInformation("[StoresController] Successfully created store with ID {StoreId}.", store.id);
                return CreatedAtAction(nameof(GetStores), new { }, storeDto);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "[StoresController] Argument error while creating store.");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[StoresController] An error occurred while creating a store.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while creating the store.");
            }
        }

        // POST /api/Stores/Categories
        [HttpPost("Categories")]
        [ProducesResponseType(typeof(StoreCategoryDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public ActionResult<StoreCategoryDto> CreateCategory([FromBody] StoreCategoryCreateDto dto)
        {
            _logger.LogInformation("[StoresController] Attempting to create a new store category with Name: {CategoryName}", dto.Name);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("[StoresController] Create category request failed model validation. Errors: {@ModelState}", ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }

            try
            {
                var existingCategory = _storeCategoryService.GetAllCategories()
                    .FirstOrDefault(c => c.name.Equals(dto.Name, StringComparison.OrdinalIgnoreCase));

                if (existingCategory != null)
                {
                    _logger.LogWarning("[StoresController] Store category with name '{CategoryName}' already exists.", dto.Name);
                    return BadRequest($"Store category with name '{dto.Name}' already exists.");
                }

                var category = _storeCategoryService.CreateCategory(dto.Name);

                var categoryDto = new StoreCategoryDto
                {
                    Id = category.id,
                    Name = category.name
                };


                _logger.LogInformation("[StoresController] Successfully created store category with ID {CategoryId}.", category.id);
                return CreatedAtAction(nameof(GetStoreCategories), new { }, categoryDto);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "[StoresController] Argument error while creating store category.");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[StoresController] An error occurred while creating a store category.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while creating the category.");
            }
        }
    }
}