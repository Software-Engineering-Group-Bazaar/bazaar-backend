using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AdminApi.DTOs;
using Loyalty.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Order.Interface;
using Order.Models;
using Store.Interface;
using Store.Models;
using Users.Models;

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
        private readonly UserManager<User> _userManager;
        private readonly IOrderService _orderService;
        private readonly ILoyaltyService _loyaltyService;

        public StoresController(
            IStoreService storeService,
            IStoreCategoryService storeCategoryService,
            UserManager<User> userManager,
            ILogger<StoresController> logger,
            IOrderService orderService,
            ILoyaltyService loyaltyService
        )
        {
            _storeService = storeService;
            _storeCategoryService = storeCategoryService;
            _userManager = userManager;
            _logger = logger;
            _orderService = orderService;
            _loyaltyService = loyaltyService;
        }

        // GET /api/Stores
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<StoreGetDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)] // Dobra praksa je dodati i ovo
        [ProducesResponseType(StatusCodes.Status403Forbidden)] // I ovo, ako korisnik nema traženu ulogu
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

                var storeDtos = stores
                    .Select(store => new StoreGetDto
                    {
                        Id = store.id,
                        Name = store.name,
                        Address = store.address,
                        Description = store.description,
                        IsActive = store.isActive,
                        CategoryName = store.category?.name ?? "N/A",
                    })
                    .ToList();

                _logger.LogInformation(
                    "[StoresController] Successfully retrieved {StoreCount} stores.",
                    storeDtos.Count
                );
                return Ok(storeDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[StoresController] An error occurred while retrieving stores."
                );
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving stores."
                );
            }
        }

        // GET /api/Stores/{id}
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(StoreGetDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)] // Dobra praksa je dodati i ovo
        [ProducesResponseType(StatusCodes.Status403Forbidden)] // I ovo, ako korisnik nema traženu ulogu
        public ActionResult<IEnumerable<StoreGetDto>> GetStoreById(int id)
        {
            _logger.LogInformation("[StoresController] Attempting to retrieve store by id.");
            if (id <= 0)
            {
                _logger.LogInformation("[StoresController] GetStoreById - Invalid id {UserId}", id);
                return NotFound($"Invalid id");
            }

            var store = _storeService.GetStoreById(id);

            if (store == null)
            {
                _logger.LogError("[StoresController] GetStoreById - Store {id} was not found.", id);
                return NotFound($"The store (ID: {id}) was not found."); // 404 Not Found
            }

            var storeDto = new StoreGetDto
            {
                Id = store.id,
                Name = store.name,
                Address = store.address,
                Description = store.description,
                IsActive = store.isActive,
                PlaceName = store.place.Name,
                RegionName = store.place.Region.Name,
                CategoryName = store.category?.name ?? "N/A",
            };

            _logger.LogInformation(
                "[StoresController] GetStoreById - Successfully retrieved store {StoreId}",
                store.id
            );

            return Ok(storeDto);
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

                var categoryDtos = categories
                    .Select(c => new StoreCategoryDto { Id = c.id, Name = c.name })
                    .ToList();

                _logger.LogInformation(
                    "[StoresController] Successfully retrieved {CategoryCount} store categories.",
                    categoryDtos.Count
                );
                return Ok(categoryDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[StoresController] An error occurred while retrieving store categories."
                );
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving store categories."
                );
            }
        }

        // POST /api/Stores (Kreiranje prodavnice i ažuriranje korisnika)
        [HttpPost]
        [Authorize(Roles = "Admin, Seller")] // Samo Admin i Seller mogu da kreiraju prodavnice? Prilagodite.
        [ProducesResponseType(typeof(StoreGetDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<StoreGetDto>> CreateStore([FromBody] StoreCreateDto dto)
        {
            // 1. Dobijanje ID-a trenutno ulogovanog korisnika
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning(
                    "[StoresController] CreateStore - Could not find user ID claim for the authenticated user."
                );
                return Unauthorized("User ID claim not found."); // 401 Unauthorized
            }

            _logger.LogInformation(
                "[StoresController] CreateStore - User {UserId} attempting to create store: Name={StoreName}, CategoryId={CategoryId}",
                userId,
                dto.Name,
                dto.CategoryId
            );

            // 2. Validacija DTO modela
            if (!ModelState.IsValid)
            {
                _logger.LogWarning(
                    "[StoresController] CreateStore - Model validation failed for User {UserId}. Errors: {@ModelState}",
                    userId,
                    ModelState.Values.SelectMany(v => v.Errors)
                );
                return BadRequest(ModelState); // 400 Bad Request
            }

            StoreModel? createdStore = null;
            try
            {
                var category = _storeCategoryService.GetCategoryById(dto.CategoryId);
                if (category == null)
                {
                    _logger.LogWarning(
                        "[StoresController] CreateStore - Category with ID {CategoryId} not found for User {UserId}.",
                        dto.CategoryId,
                        userId
                    );
                    ModelState.AddModelError(
                        nameof(dto.CategoryId),
                        $"Category with ID {dto.CategoryId} does not exist."
                    );
                    return BadRequest(ModelState); // 400 Bad Request
                }

                createdStore = _storeService.CreateStore(
                    dto.Name,
                    dto.CategoryId,
                    dto.Address,
                    dto.Description,
                    dto.PlaceId
                );
                _logger.LogInformation(
                    "[StoresController] CreateStore - Store {StoreId} created in database for User {UserId}.",
                    createdStore.id,
                    userId
                );
                // --- Kraj dela za kreiranje prodavnice ---

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogError(
                        "[StoresController] CreateStore - User {UserId} not found in database after being authenticated. Store {StoreId} was created but cannot be linked.",
                        userId,
                        createdStore.id
                    );

                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Authenticated user could not be found to associate the store. Store was created but not linked."
                    );
                }

                if (user.StoreId != null)
                {
                    _logger.LogWarning(
                        "[StoresController] CreateStore - User {UserId} already has an associated StoreId ({ExistingStoreId}). Cannot assign new StoreId {NewStoreId}.",
                        userId,
                        user.StoreId,
                        createdStore.id
                    );

                    return BadRequest(
                        $"User already has an associated store (ID: {user.StoreId}). Cannot create and link a new store."
                    );
                }

                user.StoreId = createdStore.id;
                var updateResult = await _userManager.UpdateAsync(user);

                if (!updateResult.Succeeded)
                {
                    _logger.LogError(
                        "[StoresController] CreateStore - Failed to update User {UserId} with StoreId {StoreId}. Errors: {@Errors}. Store was created but linking failed.",
                        userId,
                        createdStore.id,
                        updateResult.Errors
                    );

                    foreach (var error in updateResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        new
                        {
                            message = "Store created successfully, but failed to link it to the user.",
                            errors = ModelState,
                        }
                    );
                }

                _logger.LogInformation(
                    "[StoresController] CreateStore - Successfully updated User {UserId} with StoreId {StoreId}.",
                    userId,
                    createdStore.id
                );
                // --- Kraj dela za ažuriranje korisnika ---

                // 7. Mapiranje kreirane prodavnice u DTO za odgovor
                var storeDto = new StoreGetDto
                {
                    Id = createdStore.id,
                    Name = createdStore.name,
                    Address = createdStore.address,
                    Description = createdStore.description,
                    IsActive = createdStore.isActive,
                    PlaceName = createdStore.place.Name,
                    RegionName = createdStore.place.Region.Name,
                    CategoryName = category.name, // Koristi kategoriju dobijenu ranije
                };

                // 8. Vraćanje odgovora 201 Created
                // Ruta za GetStoreById bi bila bolja, ali GetStores je fallback.
                return CreatedAtAction(
                    nameof(GetStores),
                    new
                    { /* id = createdStore.id */
                    },
                    storeDto
                );
            }
            catch (ArgumentException ex) // Npr. ako CreateStore baci grešku
            {
                _logger.LogWarning(
                    ex,
                    "[StoresController] CreateStore - Argument error during store creation for User {UserId}.",
                    userId
                );
                // Ako je ModelState već popunjen (npr. za kategoriju), koristi ga.
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);
                return BadRequest(ex.Message); // 400 Bad Request
            }
            catch (Exception ex) // Neočekivana greška
            {
                _logger.LogError(
                    ex,
                    "[StoresController] CreateStore - An unexpected error occurred for User {UserId}.",
                    userId
                );
                // RAZMOTRITI: Ako je prodavnica kreirana pre exception-a, možda je treba obrisati?
                // if (createdStore != null) { /* ... logika za brisanje ako je potrebno ... */ }
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An unexpected error occurred while creating the store or linking it to the user."
                ); // 500 Internal Server Error
            }
        }

        // GET /api/Stores/MyStore
        [HttpGet("MyStore")]
        [ProducesResponseType(typeof(StoreGetDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<StoreGetDto>> GetMyStore()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning(
                    "[StoresController] GetMyStore - Could not find user ID claim for the authenticated user."
                );
                return Unauthorized("User ID claim not found.");
            }

            _logger.LogInformation(
                "[StoresController] GetMyStore - Attempting to retrieve store for User {UserId}",
                userId
            );

            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogError(
                        "[StoresController] GetMyStore - User {UserId} not found in database after being authenticated.",
                        userId
                    );
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Authenticated user could not be found."
                    );
                }

                if (user.StoreId == null)
                {
                    _logger.LogInformation(
                        "[StoresController] GetMyStore - User {UserId} does not have an associated store.",
                        userId
                    );
                    return NotFound($"No store associated with the current user.");
                }

                var store = _storeService.GetStoreById(user.StoreId.Value);

                if (store == null)
                {
                    _logger.LogError(
                        "[StoresController] GetMyStore - Data inconsistency: User {UserId} has StoreId {StoreId}, but the store was not found.",
                        userId,
                        user.StoreId.Value
                    );
                    return NotFound(
                        $"The store (ID: {user.StoreId.Value}) associated with the user was not found."
                    ); // 404 Not Found
                }

                var storeDto = new StoreGetDto
                {
                    Id = store.id,
                    Name = store.name,
                    Address = store.address,
                    Description = store.description,
                    IsActive = store.isActive,
                    PlaceName = store.place.Name,
                    RegionName = store.place.Region.Name,
                    CategoryName = store.category?.name ?? "N/A",
                };

                _logger.LogInformation(
                    "[StoresController] GetMyStore - Successfully retrieved store {StoreId} for User {UserId}",
                    store.id,
                    userId
                );

                return Ok(storeDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[StoresController] GetMyStore - An unexpected error occurred while retrieving store for User {UserId}.",
                    userId
                );
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An unexpected error occurred while retrieving your store."
                );
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
            _logger.LogInformation(
                "[StoresController] Attempting to create a new store category with Name: {CategoryName}",
                dto.Name
            );

            if (!ModelState.IsValid)
            {
                _logger.LogWarning(
                    "[StoresController] Create category request failed model validation. Errors: {@ModelState}",
                    ModelState.Values.SelectMany(v => v.Errors)
                );
                return BadRequest(ModelState);
            }

            try
            {
                var existingCategory = _storeCategoryService
                    .GetAllCategories()
                    .FirstOrDefault(c =>
                        c.name.Equals(dto.Name, StringComparison.OrdinalIgnoreCase)
                    );

                if (existingCategory != null)
                {
                    _logger.LogWarning(
                        "[StoresController] Store category with name '{CategoryName}' already exists.",
                        dto.Name
                    );
                    return BadRequest($"Store category with name '{dto.Name}' already exists.");
                }

                var category = _storeCategoryService.CreateCategory(dto.Name);

                var categoryDto = new StoreCategoryDto { Id = category.id, Name = category.name };

                _logger.LogInformation(
                    "[StoresController] Successfully created store category with ID {CategoryId}.",
                    category.id
                );
                return CreatedAtAction(nameof(GetStoreCategories), new { }, categoryDto);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(
                    ex,
                    "[StoresController] Argument error while creating store category."
                );
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[StoresController] An error occurred while creating a store category."
                );
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An unexpected error occurred while creating the category."
                );
            }
        }

        [HttpGet("search")] // Defines the route: /api/Stores/search
        [ProducesResponseType(typeof(IEnumerable<StoreGetDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IEnumerable<StoreGetDto>>> SearchStores(
            [FromQuery] string query = ""
        ) // Takes query param from URL
        {
            _logger.LogInformation(
                "[StoresController] Attempting to search stores with query: '{Query}'",
                query
            );

            try
            {
                var stores = await _storeService.SearchStoresAsync(query);

                if (stores == null || !stores.Any())
                {
                    _logger.LogInformation(
                        "[StoresController] No stores found matching query: '{Query}'",
                        query
                    );
                    return Ok(new List<StoreGetDto>()); // Return empty list for no results
                }

                // Map StoreModel results to StoreGetDto
                var storeDtos = stores
                    .Select(store => new StoreGetDto
                    {
                        Id = store.id,
                        Name = store.name,
                        Address = store.address,
                        Description = store.description,
                        IsActive = store.isActive,
                        PlaceName = store.place.Name,
                        RegionName = store.place.Region.Name,
                        CategoryName = store.category?.name ?? "N/A", // Safely access category name
                    })
                    .ToList();

                _logger.LogInformation(
                    "[StoresController] Successfully found {StoreCount} stores matching query: '{Query}'",
                    storeDtos.Count,
                    query
                );
                return Ok(storeDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[StoresController] An error occurred while searching stores with query: '{Query}'",
                    query
                );
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while searching for stores."
                );
            }
        }

        // NEW ENDPOINT: GET api/Stores/income
        [HttpGet("income")]
        [Authorize(Roles = "Seller, Admin")] // Only Seller or Admin can access their store's income
        [ProducesResponseType(typeof(AdminApi.DTOs.StoreIncomeDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AdminApi.DTOs.StoreIncomeDto>> GetMyStoreIncome(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to
        )
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning(
                    "[StoresController] GetMyStoreIncome - Unauthorized: User ID claim not found."
                );
                return Unauthorized("User ID claim not found.");
            }

            _logger.LogInformation(
                "[StoresController] GetMyStoreIncome - User {UserId} attempting to retrieve income for their store from {FromDate} to {ToDate}",
                userId,
                from,
                to
            );

            if (from > to)
            {
                _logger.LogWarning(
                    "[StoresController] GetMyStoreIncome - BadRequest: 'from' date ({FromDate}) cannot be after 'to' date ({ToDate}) for User {UserId}",
                    from,
                    to,
                    userId
                );
                return BadRequest("'from' date cannot be after 'to' date.");
            }

            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogError(
                        "[StoresController] GetMyStoreIncome - InternalError: Authenticated User {UserId} not found in database.",
                        userId
                    );
                    // This case should ideally not happen if authentication is working.
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "Authenticated user could not be found."
                    );
                }

                if (user.StoreId == null)
                {
                    _logger.LogWarning(
                        "[StoresController] GetMyStoreIncome - NotFound: User {UserId} does not have an associated store.",
                        userId
                    );
                    return NotFound(
                        "No store is associated with your account. Cannot calculate income."
                    );
                }

                int storeId = user.StoreId.Value;
                var store = _storeService.GetStoreById(storeId); // Assuming this is synchronous, adjust if async
                if (store == null)
                {
                    // This would indicate data inconsistency if user.StoreId is set but store doesn't exist.
                    _logger.LogError(
                        "[StoresController] GetMyStoreIncome - NotFound: Store with ID {StoreId} associated with User {UserId} not found.",
                        storeId,
                        userId
                    );
                    return NotFound(
                        $"The store (ID: {storeId}) associated with your account was not found."
                    );
                }

                var orders = await _orderService.GetOrdersByStoreAsync(storeId);

                // Define the date range carefully to include the whole 'to' day.
                DateTime fromDateStartOfDay = from.Date;
                DateTime toDateEndOfDay = to.Date.AddDays(1).AddTicks(-1);

                var filteredOrders =
                    orders
                        ?.Where(o => o.Time >= fromDateStartOfDay && o.Time <= toDateEndOfDay)
                        .ToList() ?? new List<OrderModel>();

                if (!filteredOrders.Any())
                {
                    _logger.LogInformation(
                        "[StoresController] GetMyStoreIncome - No orders found for Store ID {StoreId} (User {UserId}) within the date range {FromDate} - {ToDate}. Income is 0.",
                        storeId,
                        userId,
                        fromDateStartOfDay,
                        toDateEndOfDay
                    );
                    return Ok(
                        new AdminApi.DTOs.StoreIncomeDto
                        {
                            StoreId = storeId,
                            StoreName = store.name,
                            FromDate = from,
                            ToDate = to,
                            TotalIncome = 0,
                        }
                    );
                }

                decimal totalIncome = filteredOrders.Sum(o => o.Total ?? 0m);

                var incomeDto = new AdminApi.DTOs.StoreIncomeDto
                {
                    StoreId = storeId,
                    StoreName = store.name,
                    FromDate = from,
                    ToDate = to,
                    TotalIncome = totalIncome,
                };

                _logger.LogInformation(
                    "[StoresController] GetMyStoreIncome - Successfully retrieved income for Store ID {StoreId} (User {UserId}): {TotalIncome} from {FromDate} to {ToDate}",
                    storeId,
                    userId,
                    totalIncome,
                    from,
                    to
                );
                return Ok(incomeDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[StoresController] GetMyStoreIncome - An error occurred while retrieving income for User {UserId}'s store.",
                    userId
                );
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An internal error occurred while calculating your store's income."
                );
            }
        }

        [HttpGet("points")]
        public async Task<ActionResult<int>> GetPointsDistributedAsync(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to
        )
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);
            if (user is null || user.StoreId is null)
                return StatusCode(StatusCodes.Status400BadRequest, "You dont have a store!");
            int t = await _loyaltyService.GetStorePointsAssigned((int)user.StoreId, from, to);
            return Ok(t);
        }
    }
}
