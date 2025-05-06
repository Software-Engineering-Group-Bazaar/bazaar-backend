using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdminApi.DTOs; // Your DTOs namespace (Ensure this namespace is correct)
using Catalog.Dtos;
using Catalog.Models;
using Catalog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging; // Required for logging
using Notifications.Interfaces;
using Order.Interface;
using Order.Models;
using SharedKernel;
using Store.Interface;
using Store.Models;
using Users.Models; // Your User model and DbContext namespace
namespace Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<AdminController> _logger; // Inject logger

        private readonly IProductService _productService;
        private readonly IStoreService _storeService;
        private readonly IStoreCategoryService _storeCategoryService;
        private readonly IProductCategoryService _productCategoryService;
        private readonly IOrderService _orderService;         // <<<--- INJECT
        private readonly IOrderItemService _orderItemService; // <<<--- INJECT
        private readonly IPushNotificationService _pushNotificationService;
        private readonly INotificationService _notificationService;

        public AdminController(
            UserManager<User> userManager,
            RoleManager<IdentityRole> roleManager,
            IProductService productService,
            IStoreService storeService,
            IStoreCategoryService storeCategoryService,
            IProductCategoryService productCategoryService,
            ILogger<AdminController> logger,
            IOrderService orderService,         // <<<--- ADD to constructor parameters
            IPushNotificationService pushNotificationService,
            INotificationService notificationService,
            IOrderItemService orderItemService) // Add logger to constructor

        {
            _userManager = userManager;
            _roleManager = roleManager;
            _productService = productService;
            _storeService = storeService;
            _productCategoryService = productCategoryService;
            _storeCategoryService = storeCategoryService;
            _logger = logger; // Assign injected logger
            _orderService = orderService;         // <<<--- ASSIGN injected service
            _pushNotificationService = pushNotificationService;
            _notificationService = notificationService;
            _orderItemService = orderItemService;
        }

        // GET /api/admin/users
        [HttpGet("users")]
        [ProducesResponseType(typeof(IEnumerable<UserInfoDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<UserInfoDto>>> GetUsers()
        {
            _logger.LogInformation("Attempting to retrieve all users.");
            try
            {
                var users = await _userManager.Users.ToListAsync();
                var userInfoDtos = new List<UserInfoDto>();
                foreach (var user in users)
                {
                    userInfoDtos.Add(new UserInfoDto
                    {
                        Id = user.Id,
                        UserName = user.UserName ?? "N/A",
                        Email = user.Email ?? "N/A",
                        EmailConfirmed = user.EmailConfirmed,
                        Roles = await _userManager.GetRolesAsync(user), // Be mindful of performance on very large user sets
                        IsApproved = user.IsApproved,
                        IsActive = user.IsActive,
                        CreatedAt = user.CreatedAt
                    });
                }
                _logger.LogInformation("Successfully retrieved {UserCount} users.", userInfoDtos.Count);
                return Ok(userInfoDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving users.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred while retrieving users.");
            }
        }

        // POST /api/admin/users/create
        [HttpPost("users/create")]
        [ProducesResponseType(typeof(UserInfoDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<UserInfoDto>> CreateUser([FromBody] CreateUserDto createUserDto)
        {
            _logger.LogInformation("Attempting to create a new user with UserName: {UserName}, Email: {Email}", createUserDto.UserName, createUserDto.Email);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Create user request failed model validation. Errors: {@ModelState}", ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }

            // Check if username or email already exists
            var existingUserByName = await _userManager.FindByNameAsync(createUserDto.UserName);
            if (existingUserByName != null)
            {
                _logger.LogWarning("Username '{UserName}' is already taken.", createUserDto.UserName);
                return BadRequest($"Username '{createUserDto.UserName}' is already taken.");
            }
            var existingUserByEmail = await _userManager.FindByEmailAsync(createUserDto.Email);
            if (existingUserByEmail != null)
            {
                _logger.LogWarning("Email '{Email}' is already registered.", createUserDto.Email);
                return BadRequest($"Email '{createUserDto.Email}' is already registered.");
            }

            var user = new User
            {
                UserName = createUserDto.UserName,
                Email = createUserDto.Email,
                IsApproved = true, // Setting IsApproved for admin-created users
                EmailConfirmed = false // Or true if you want admin-created users to be confirmed
            };

            var result = await _userManager.CreateAsync(user, createUserDto.Password);
            if (!result.Succeeded)
            {
                _logger.LogError("User creation failed for UserName {UserName}. Errors: {@IdentityErrors}", user.UserName, result.Errors);
                AddErrors(result);
                return BadRequest(ModelState);
            }
            _logger.LogInformation("User {UserName} (ID: {UserId}) created successfully.", user.UserName, user.Id);

            // Add the user to the specified role
            _logger.LogInformation("Attempting to add user {UserId} to role {UserRole}", user.Id, Role.Seller.ToString());
            var roleResult = await _userManager.AddToRoleAsync(user, Utils.FirstLetterToUpper(createUserDto.Role));
            if (!roleResult.Succeeded)
            {
                _logger.LogError("Failed to add user {UserId} to role {UserRole}. Errors: {@IdentityErrors}", user.Id, Role.Seller.ToString(), roleResult.Errors);
                // Clean up user if role assignment fails
                await _userManager.DeleteAsync(user);
                _logger.LogInformation("Rolled back creation of user {UserId} due to role assignment failure.", user.Id);
                AddErrors(roleResult);
                ModelState.AddModelError(string.Empty, $"Failed to assign role '{Role.Seller.ToString()}'. User creation rolled back.");
                return BadRequest(ModelState);
            }
            _logger.LogInformation("Successfully added user {UserId} to role {UserRole}", user.Id, Role.Seller.ToString());


            // Return the created user's info
            var userInfo = new UserInfoDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                EmailConfirmed = user.EmailConfirmed,
                Roles = await _userManager.GetRolesAsync(user),
                IsApproved = user.IsApproved
            };

            return CreatedAtAction(nameof(GetUsers), new { }, userInfo);
        }

        // POST /api/admin/users/create
        [HttpPut("users/update")]
        [ProducesResponseType(typeof(UserInfoDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<UserInfoDto>> UpdateUser([FromBody] UpdateUserDto dto)
        {
            var user = await _userManager.FindByIdAsync(dto.Id);
            if (user == null)
                return NotFound("User not found");


            user.Email = dto.Email;
            user.UserName = dto.UserName;
            user.Id = dto.Id;
            user.IsActive = dto.IsActive;
            user.IsApproved = dto.IsApproved;

            var userInfo = new UserInfoDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                EmailConfirmed = user.EmailConfirmed,
                Roles = new List<string> { dto.Role },
                IsApproved = user.IsApproved,
                IsActive = user.IsActive
            };

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
                return CreatedAtAction(nameof(GetUsers), new { }, userInfo);

            return BadRequest(result.Errors);

        }

        // POST /api/admin/users/approve
        [HttpPost("users/approve")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)] // Updated success response type
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ApproveUser([FromBody] ApproveUserDto approveUserDto)
        {
            _logger.LogInformation("Attempting to approve user with ID: {UserId}", approveUserDto.UserId);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Approve user request failed model validation for User ID {UserId}. Errors: {@ModelState}", approveUserDto.UserId, ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }

            var user = await _userManager.FindByIdAsync(approveUserDto.UserId);
            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found for approval.", approveUserDto.UserId);
                return NotFound($"User with ID {approveUserDto.UserId} not found.");
            }

            if (user.IsApproved)
            {
                _logger.LogWarning("User {UserId} is already approved.", approveUserDto.UserId);
                return BadRequest($"User with ID {approveUserDto.UserId} is already approved.");
            }

            user.IsApproved = true;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                _logger.LogError("Failed to update user {UserId} for approval. Errors: {@IdentityErrors}", user.Id, result.Errors);
                AddErrors(result);
                return BadRequest(ModelState); // Or return a 500 Internal Server Error
            }

            _logger.LogInformation("User {UserId} approved successfully.", user.Id);
            return Ok($"User {user.UserName ?? user.Id} successfully approved."); // Return 200 OK with a message
        }

        // POST /api/admin/users/approve
        [HttpPost("users/activate")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)] // Updated success response type
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ActivateUser([FromBody] ActivateUserDto dto)
        {
            _logger.LogInformation("Attempting to activate user with ID: {UserId}", dto.UserId);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Activate user request failed model validation for User ID {UserId}. Errors: {@ModelState}", dto.UserId, ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }

            var user = await _userManager.FindByIdAsync(dto.UserId);
            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found for approval.", dto.UserId);
                return NotFound($"User with ID {dto.UserId} not found.");
            }

            if (!user.IsApproved)
            {
                _logger.LogWarning("User {UserId} is not already approved.", dto.UserId);
                return BadRequest($"User with ID {dto.UserId} is not already approved.");
            }

            user.IsActive = dto.ActivationStatus;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                _logger.LogError("Failed to update user {UserId} for approval. Errors: {@IdentityErrors}", user.Id, result.Errors);
                AddErrors(result);
                return BadRequest(ModelState); // Or return a 500 Internal Server Error
            }

            _logger.LogInformation("User {UserId} approved successfully.", user.Id);
            return Ok($"User {user.UserName ?? user.Id} successfully approved."); // Return 200 OK with a message
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
                _logger.LogInformation("Attempting to create product with storeID: {StoreId} categoryID:{CategoryId}", createProductDto.StoreId, createProductDto.ProductCategoryId);

                var store = _storeService.GetStoreById(createProductDto.StoreId);

                if (store is null)
                {
                    _logger.LogInformation($"not found store with id:{createProductDto.StoreId}");
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
                    WholesalePrice = createProductDto.WholesalePrice,
                    Weight = createProductDto.Weight,
                    WeightUnit = createProductDto.WeightUnit,
                    Volume = createProductDto.Volume,
                    VolumeUnit = createProductDto.VolumeUnit,
                    StoreId = createProductDto.StoreId
                };

                var createdProduct = await _productService.CreateProductAsync(product, createProductDto.Files);
                _logger.LogInformation("Succesfully created a product with id {ProductId} {ProductName}", createdProduct.Id, createdProduct.Name);
                var createdProductDto = new AdminApi.DTOs.ProductGetDto
                {
                    Id = product.Id,
                    Name = product.Name,
                    ProductCategory = new ProductCategoryGetDto { Id = product.ProductCategory.Id, Name = product.ProductCategory.Name },
                    RetailPrice = product.RetailPrice,
                    WholesalePrice = product.WholesalePrice,
                    Weight = product.Weight,
                    WeightUnit = product.WeightUnit,
                    Volume = product.Volume,
                    VolumeUnit = product.VolumeUnit,
                    StoreId = product.StoreId,
                    CreatedAt = product.CreatedAt,
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

        // DELETE /api/admin/user/{id}
        [HttpDelete("user/{id}")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)] // Updated success response type
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteUser(string id)
        {
            _logger.LogInformation("Attempting to delete user with ID: {UserId}", id);

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found for deletion.", id);
                return NotFound($"User with ID {id} not found.");
            }

            _logger.LogInformation("Found user {UserName} (ID: {UserId}) for deletion.", user.UserName, id);
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                _logger.LogError("Failed to delete user {UserId}. Errors: {@IdentityErrors}", id, result.Errors);
                AddErrors(result);
                return BadRequest(ModelState); // Or return a 500 Internal Server Error
            }

            _logger.LogInformation("User {UserId} deleted successfully.", id);
            // Consider returning 204 NoContent as per REST standards for DELETE,
            // but returning 200 OK with a message is also acceptable and sometimes preferred for clarity.
            return Ok($"User with ID {id} successfully deleted.");
        }



        // GET /api/Admin/stores
        [HttpGet("stores")]
        [ProducesResponseType(typeof(IEnumerable<StoreDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<IEnumerable<StoreDto>> GetStores()
        {
            _logger.LogInformation("Attempting to retrieve all stores.");

            try
            {
                var stores = _storeService.GetAllStores(); // kasnije prebaciti u asinhrono
                if (stores == null || !stores.Any())
                {
                    _logger.LogInformation("No stores found.");
                    return Ok(new List<StoreDto>());
                }

                var storeDtos = stores.Select(store => new StoreDto
                {
                    Id = store.id,
                    Name = store.name,
                    Address = store.address,
                    Description = store.description,
                    IsActive = store.isActive,
                    CategoryName = store.category.name,
                    CreatedAt = store.createdAt,
                    PlaceName = store.place.Name,
                    RegionName = store.place.Region.Name
                }).ToList();

                _logger.LogInformation("Successfully retrieved {StoreCount} stores.", storeDtos.Count);
                return Ok(storeDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving stores.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving stores.");
            }
        }
        // GET /api/Admin/stores/{id}
        [HttpGet("stores/{id}")]
        [ProducesResponseType(typeof(IEnumerable<StoreDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<IEnumerable<StoreDto>> GetStoresById(int id)
        {
            _logger.LogInformation($"Attempting to retrieve store. {id}");

            try
            {
                var stores = _storeService.GetStoreById(id);
                if (stores is null)
                {
                    _logger.LogInformation("No storee found.");
                    return BadRequest("No store found");
                }

                var storeDto = new StoreDto
                {
                    Id = id,
                    Address = stores.address,
                    CategoryName = stores.category.name,
                    IsActive = stores.isActive,
                    Description = stores.description,
                    Name = stores.name,
                    PlaceName = stores.place.Name,
                    CreatedAt = stores.createdAt,
                    RegionName = stores.place.Region.Name
                };

                return Ok(storeDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving stores.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving stores.");
            }
        }


        // GET /api/Admin/store/categories
        [HttpGet("store/categories")]
        [ProducesResponseType(typeof(IEnumerable<StoreCategoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<IEnumerable<StoreCategoryDto>> GetStoreCategories()
        {
            _logger.LogInformation("Attempting to retrieve store categories.");

            try
            {
                var categories = _storeCategoryService.GetAllCategories();
                if (categories == null || !categories.Any())
                {
                    _logger.LogInformation("No categories found.");
                    return Ok(new List<StoreCategoryDto>());
                }

                var categoryDtos = categories.Select(c => new StoreCategoryDto
                {
                    Id = c.id,
                    Name = c.name
                }).ToList();

                _logger.LogInformation("Successfully retrieved {CategoryCount} categories.", categoryDtos.Count);
                return Ok(categoryDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving store categories.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving store categories.");
            }
        }


        // PUT /api/Admin/store/{id}
        [HttpPut("store/{id}")]
        [ProducesResponseType(typeof(StoreDto), StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<StoreDto> UpdateStore([FromBody] StoreUpdateDto dto)
        {
            _logger.LogInformation("Attempting to update a store.");

            try
            {
                if (dto.CategoryId is not null)
                {
                    var category = _storeCategoryService.GetCategoryById((int)dto.CategoryId);
                    if (category == null)
                    {
                        _logger.LogWarning("Category with ID {CategoryId} not found.", dto.CategoryId);
                        return BadRequest($"Category with ID {dto.CategoryId} does not exist.");
                    }
                }

                var store = _storeService.GetStoreById(dto.Id);

                if (store is null)
                {
                    return BadRequest($"Store with ID {dto.Id} does not exist!");
                }

                _storeService.UpdateStore(dto.Id, dto.Name, dto.CategoryId, dto.Address, dto.Description, dto.IsActive);

                _logger.LogInformation("Successfully created store with ID {StoreId}.", store.id);
                return CreatedAtAction(nameof(GetStores), new { id = store.id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating a store.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the store.");
            }
        }

        // POST /api/Admin/store/create
        [HttpPost("store/create")]
        [ProducesResponseType(typeof(StoreDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<StoreDto> CreateStore([FromBody] StoreCreateDto dto)
        {
            _logger.LogInformation("Attempting to create a new store.");

            try
            {
                var category = _storeCategoryService.GetCategoryById(dto.CategoryId);
                if (category == null)
                {
                    _logger.LogWarning("Category with ID {CategoryId} not found.", dto.CategoryId);
                    return BadRequest($"Category with ID {dto.CategoryId} does not exist.");
                }

                var store = _storeService.CreateStore(dto.Name, dto.CategoryId, dto.Address, dto.Description, dto.PlaceId);

                var storeDto = new StoreDto
                {
                    Id = store.id,
                    Name = store.name,
                    Address = store.address,
                    Description = store.description,
                    IsActive = store.isActive,
                    CategoryName = category.name,
                    PlaceName = store.place.Name,
                    CreatedAt = store.createdAt,
                    RegionName = store.place.Region.Name
                };

                _logger.LogInformation("Successfully created store with ID {StoreId}.", store.id);
                return CreatedAtAction(nameof(GetStores), new { id = store.id }, storeDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating a store.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the store.");
            }
        }

        [HttpDelete("store/{id}")] // DELETE /api/catalog/products/15
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteStore(int id)
        {
            if (id <= 0) return BadRequest("Invalid product ID.");

            try
            {
                var success = await _storeService.DeleteStoreAsync(id);
                await _productService.DeleteProductFromStoreAsync(id);
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


        //DELETE api/Admin/store/category/{id}
        [HttpPut("store/category/{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateStoreCategory(int id, [FromBody] ProductCategoryDto category)
        {
            if (id <= 0 || category == null)
            {
                return BadRequest("Route ID mismatch with body ID or invalid ID provided.");
            }

            try
            {
                var categoryOld = _storeCategoryService.GetCategoryById(id);

                if (categoryOld == null)
                {
                    return NotFound();
                }

                categoryOld.name = category.Name;

                var success = _storeCategoryService.UpdateCategory(categoryOld.id, category.Name);

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

        //DELETE api/Admin/store/category/{id}
        [HttpDelete("store/category/{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteStoreCategory(int id)
        {
            if (id <= 0) return BadRequest("Invalid category ID.");

            try
            {
                var success = _storeCategoryService.DeleteCategory(id);

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


        // POST /api/Admin/store/categories/create
        [HttpPost("store/categories/create")]
        [ProducesResponseType(typeof(StoreCategory), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<StoreCategory> CreateCategory([FromBody] StoreCategoryCreateDto dto)
        {
            _logger.LogInformation("Attempting to create a new store category.");

            try
            {
                // Provjera da li već postoji kategorija sa istim imenom
                var existingCategory = _storeCategoryService.GetAllCategories()
                    .FirstOrDefault(c => c.name.Equals(dto.Name, StringComparison.OrdinalIgnoreCase));

                if (existingCategory != null)
                {
                    _logger.LogWarning("Category with name '{CategoryName}' already exists.", dto.Name);
                    return BadRequest("Category with this name already exists.");
                }

                // Kreiranje nove kategorije
                var category = _storeCategoryService.CreateCategory(dto.Name);

                _logger.LogInformation("Successfully created category with ID {CategoryId}.", category.id);
                return CreatedAtAction(nameof(GetStoreCategories), new { id = category.id }, category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating a category.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the category.");
            }
        }


        // --- Akcije za Kategorije (ProductCategory) ---

        [HttpGet("categories")] // GET /api/catalog/categories
        [ProducesResponseType(typeof(IEnumerable<ProductCategoryGetDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllCategories()
        {
            var categories = await _productCategoryService.GetAllCategoriesAsync();

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

            var category = await _productCategoryService.GetCategoryByIdAsync(id);
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

                var createdCategory = await _productCategoryService.CreateCategoryAsync(productCategory);

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
                var categoryOld = await _productCategoryService.GetCategoryByIdAsync(id);

                if (categoryOld == null)
                {
                    return NotFound();
                }

                categoryOld.Name = category.Name;

                var success = await _productCategoryService.UpdateCategoryAsync(categoryOld);

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
                var success = await _productCategoryService.DeleteCategoryAsync(id);

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
        [ProducesResponseType(typeof(IEnumerable<AdminApi.DTOs.ProductGetDto>), StatusCodes.Status200OK)]
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

            var productsDto = products.Select(product => new AdminApi.DTOs.ProductGetDto
            {
                Id = product.Id,
                Name = product.Name,
                ProductCategory = new ProductCategoryGetDto { Id = product.ProductCategory.Id, Name = product.ProductCategory.Name },
                RetailPrice = product.RetailPrice,
                WholesalePrice = product.WholesalePrice,
                Weight = product.Weight,
                WeightUnit = product.WeightUnit,
                Volume = product.Volume,
                VolumeUnit = product.VolumeUnit,
                StoreId = product.StoreId,
                IsActive = product.IsActive,
                CreatedAt = product.CreatedAt,
                Photos = product.Pictures.Select(photo => photo.Url).ToList()
            }).ToList();

            return Ok(productsDto);
        }

        [HttpGet("products/{id}")] // GET /api/catalog/products/15
        [ProducesResponseType(typeof(AdminApi.DTOs.ProductGetDto), StatusCodes.Status200OK)]
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

            var productDto = new AdminApi.DTOs.ProductGetDto
            {
                Id = product.Id,
                Name = product.Name,
                ProductCategory = new ProductCategoryGetDto { Id = product.ProductCategory.Id, Name = product.ProductCategory.Name },
                RetailPrice = product.RetailPrice,
                WholesalePrice = product.WholesalePrice,
                Weight = product.Weight,
                WeightUnit = product.WeightUnit,
                Volume = product.Volume,
                VolumeUnit = product.VolumeUnit,
                StoreId = product.StoreId,
                IsActive = product.IsActive,
                CreatedAt = product.CreatedAt,
                Photos = product.Pictures.Select(photo => photo.Url).ToList()
            };

            return Ok(productDto);
        }

        [HttpPut("products/{id}")] // PUT /api/catalog/products/15
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] UpdateProductDto productDto)
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
                var category = await _productCategoryService.GetCategoryByIdAsync(productDto.ProductCategoryId);
                if (category == null) return BadRequest($"Category with ID {productDto.ProductCategoryId} not found.");

                var product = await _productService.GetProductByIdAsync(id);
                if (product == null) return BadRequest($"Product with ID {id} not found.");


                product.Name = productDto.Name;
                product.ProductCategory = category;
                product.RetailPrice = productDto.RetailPrice;
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

        // ==================================================
        // ORDER MANAGEMENT Endpoints
        // ==================================================

        // GET /api/admin/order
        [HttpGet("order")]
        [ProducesResponseType(typeof(IEnumerable<OrderGetDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<OrderGetDto>>> GetAllOrders()
        {
            _logger.LogInformation("Attempting to retrieve all orders.");
            try
            {
                var orders = await _orderService.GetAllOrdersAsync();

                var orderDtos = orders.Select(o => new OrderGetDto
                {
                    Id = o.Id,
                    BuyerId = o.BuyerId,
                    StoreId = o.StoreId,
                    Status = o.Status.ToString(),
                    Time = o.Time,
                    Total = o.Total,
                    OrderItems = o.OrderItems.Select(oi => new OrderItemGetDto
                    {
                        Id = oi.Id,
                        ProductId = oi.ProductId,
                        Price = oi.Price,
                        Quantity = oi.Quantity
                    }).ToList()
                }).ToList();

                _logger.LogInformation("Successfully retrieved {OrderCount} orders.", orderDtos.Count);
                return Ok(orderDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving all orders.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred while retrieving orders.");
            }
        }

        // GET /api/admin/order/{id}
        [HttpGet("order/{id}")]
        [ProducesResponseType(typeof(OrderGetDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<OrderGetDto>> GetOrderById(int id)
        {
            _logger.LogInformation("Attempting to retrieve order with ID: {OrderId}", id);
            if (id <= 0)
            {
                _logger.LogWarning("GetOrderById request failed validation: Invalid ID {OrderId}", id);
                return BadRequest("Invalid Order ID provided.");
            }

            try
            {
                var order = await _orderService.GetOrderByIdAsync(id);

                if (order == null)
                {
                    _logger.LogWarning("Order with ID {OrderId} not found.", id);
                    return NotFound($"Order with ID {id} not found.");
                }

                var orderDto = new OrderGetDto
                {
                    Id = order.Id,
                    BuyerId = order.BuyerId,
                    StoreId = order.StoreId,
                    Status = order.Status.ToString(),
                    Time = order.Time,
                    Total = order.Total,
                    OrderItems = order.OrderItems.Select(oi => new OrderItemGetDto
                    {
                        Id = oi.Id,
                        ProductId = oi.ProductId,
                        Price = oi.Price,
                        Quantity = oi.Quantity
                    }).ToList()
                };

                _logger.LogInformation("Successfully retrieved order with ID: {OrderId}", id);
                return Ok(orderDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving order with ID: {OrderId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred while retrieving the order.");
            }
        }

        // POST /api/admin/order/create
        [HttpPost("order/create")]
        [ProducesResponseType(typeof(OrderGetDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<OrderGetDto>> CreateOrder([FromBody] OrderCreateDto createDto)
        {
            _logger.LogInformation("Attempting to create a new order for BuyerId: {BuyerId}, StoreId: {StoreId}", createDto.BuyerId, createDto.StoreId);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Create order request failed model validation. Errors: {@ModelState}", ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }

            // Optional: Add validation to check if BuyerId and StoreId actually exist
            // var buyerExists = await _userManager.FindByIdAsync(createDto.BuyerId) != null;
            // var storeExists = _storeService.GetStoreById(createDto.StoreId) != null; // Assuming synchronous version exists or make async
            // if (!buyerExists) return BadRequest($"Buyer with ID {createDto.BuyerId} not found.");
            // if (!storeExists) return BadRequest($"Store with ID {createDto.StoreId} not found.");

            try
            {
                var createdOrder = await _orderService.CreateOrderAsync(createDto.BuyerId, createDto.StoreId);
                var listitems = new List<OrderItemGetDto>();
                foreach (var item in createDto.OrderItems)
                {
                    var x = await _orderItemService.CreateOrderItemAsync(createdOrder.Id, item.ProductId, item.Quantity);
                    listitems.Add(new OrderItemGetDto
                    {
                        Id = x.Id,
                        ProductId = x.ProductId,
                        Price = x.Price,
                        Quantity = x.Quantity,
                    });
                }
                // Map the created order (which won't have items yet) to the DTO
                var orderDto = new OrderGetDto
                {
                    Id = createdOrder.Id,
                    BuyerId = createdOrder.BuyerId,
                    StoreId = createdOrder.StoreId,
                    Status = createdOrder.Status.ToString(),
                    Time = createdOrder.Time,
                    Total = createdOrder.Total, // Will likely be null or 0 initially
                    OrderItems = listitems // Empty list
                };

                _logger.LogInformation("Successfully created order with ID: {OrderId}", createdOrder.Id);
                int id = createdOrder.Id;
                string status = createdOrder.Status.ToString();
                // Return 201 Created with the location of the newly created resource and the resource itself
                if (createDto.BuyerId is not null)
                {
                    var buyer = await _userManager.FindByIdAsync(createDto.BuyerId);

                    await _notificationService.CreateNotificationAsync(
                            buyer.Id,
                            $"Nova narudžba #{id} je kreirana za Vas!",
                            id
                        );
                    _logger.LogInformation("Notification creation task initiated for Buyer {SellerUserId} for new Order {OrderId}.", buyer.Id, id);
                    string notificationMessage = $"Nova narudžba #{id} je kreirana za Vas!";
                    string pushTitle = "Status Narudžbe Kreiran";
                    string pushBody = $"Status narudžbe #{id} je sada: {status}.";
                    // Opcionalno: Dodaj podatke za navigaciju u aplikaciji
                    var pushData = new Dictionary<string, string> {
                                         { "orderId", id.ToString() },
                                         { "screen", "OrderDetail" } // Primjer
                                     };
                    if (buyer is null) return CreatedAtAction(nameof(GetOrderById), new { id = createdOrder.Id }, orderDto);
                    // Pozovi servis za slanje PUSH notifikacije
                    if (buyer.FcmDeviceToken is not null)
                        await _pushNotificationService.SendPushNotificationAsync(
                            buyer.FcmDeviceToken,
                            pushTitle,
                            pushBody,
                            pushData
                        );
                    _logger.LogInformation("Push Notification task initiated for Buyer {BuyerId} for Order {OrderId} status update.", buyer.Id, id);
                }
                if (createDto.StoreId > -1)
                {
                    var seller = await _userManager.Users.FirstOrDefaultAsync(u => u.StoreId == createDto.StoreId);
                    await _notificationService.CreateNotificationAsync(
                           seller.Id,
                           $"Nova narudžba #{id} je kreirana za vašu prodavnicu.",
                           id
                       );
                    string notificationMessage = $"Status Vaše narudžbe #{id} je ažuriran na '{status}'.";
                    string pushTitle = "Status Narudžbe Kreiran";
                    string pushBody = $"Status narudžbe #{id} je sada: {status}.";
                    // Opcionalno: Dodaj podatke za navigaciju u aplikaciji
                    if (seller is null) return CreatedAtAction(nameof(GetOrderById), new { id = createdOrder.Id }, orderDto);
                    var pushData = new Dictionary<string, string> {
                                         { "orderId", id.ToString() },
                                         { "screen", "OrderDetail" } // Primjer
                                     };

                    // Pozovi servis za slanje PUSH notifikacije
                    if (seller.FcmDeviceToken is not null)
                        await _pushNotificationService.SendPushNotificationAsync(
                            seller.FcmDeviceToken,
                            pushTitle,
                            pushBody,
                            pushData
                        );
                    _logger.LogInformation("Push Notification task initiated for Seller {BuyerId} for Order {OrderId} status update.", seller.Id, id);
                }
                return CreatedAtAction(nameof(GetOrderById), new { id = createdOrder.Id }, orderDto);
            }
            catch (ArgumentException ex) // Catch specific exceptions from the service if possible
            {
                _logger.LogWarning(ex, "Failed to create order due to invalid arguments (BuyerId: {BuyerId}, StoreId: {StoreId})", createDto.BuyerId, createDto.StoreId);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating an order for BuyerId: {BuyerId}, StoreId: {StoreId}", createDto.BuyerId, createDto.StoreId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred while creating the order.");
            }
        }

        // PUT /api/admin/order/update/{id} --> update the whole thing
        [HttpPut("order/update/{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateOrder(int id, [FromBody] OrderUpdateDto updateDto)
        {
            // ako znm da ce neki item fail uraditi onda necu ni glavni update raditi
            // also ako trb dodati item dodaj ga
            if (updateDto.OrderItems is not null)
            {
                try
                {
                    _logger.LogInformation("Attempting to check order validity for order items");
                    foreach (var item in updateDto.OrderItems)
                    {
                        var f = await _orderItemService.CheckValid(item.Id, item.Quantity, item.ProductId, item.Price);
                        if (!f)
                        {
                            var orderitem = await _orderItemService.CreateOrderItemAsync(id, item.ProductId, item.Quantity);
                            item.Id = orderitem.Id;
                            //_logger.LogInformation($"")
                        }
                    }
                }
                catch (ArgumentException a)
                {
                    return BadRequest($"OrderItem data invalid. {a.Message}");
                }
            }


            _logger.LogInformation("Attempting to update order for order ID: {OrderId}", id);
            OrderStatus status = OrderStatus.Requested;
            if (updateDto.Status is not null)
            {
                if (Enum.TryParse(updateDto.Status, true, out OrderStatus result))
                {
                    status = result;
                }
                else
                {
                    return BadRequest($"Nevalidan status {updateDto.Status}");
                }
            }

            var success = await _orderService.UpdateOrderAsync(id, updateDto.BuyerId, updateDto.StoreId, status, updateDto.Time, updateDto.Total);
            if (!success)
            {
                return BadRequest("Order update failed.");
            }
            if (updateDto.OrderItems is not null)
            {
                var tasks = updateDto.OrderItems.Select(item =>
                    _orderItemService.ForceUpdateOrderItemAsync(item.Id, item.Quantity, item.ProductId, item.Price)
                );
                var results = await Task.WhenAll(tasks);
                var fail = results.Any(flag => !flag);
                if (fail)
                {
                    return BadRequest("OrderItem update failed.");
                }
            }
            if (updateDto.BuyerId is not null)
            {
                var buyer = await _userManager.FindByIdAsync(updateDto.BuyerId);

                await _notificationService.CreateNotificationAsync(
                        buyer.Id,
                        $"Status Vaše narudžbe #{id} je ažuriran na '{status}'.",
                        id
                    );
                _logger.LogInformation("Notification creation task initiated for Buyer {SellerUserId} for new Order {OrderId}. samo ja logove gledam svakako", buyer.Id, id);
                string notificationMessage = $"Status Vaše narudžbe #{id} je ažuriran na '{status}'.";
                string pushTitle = "Status Narudžbe Ažuriran";
                string pushBody = $"Status narudžbe #{id} je sada: {status}.";
                // Opcionalno: Dodaj podatke za navigaciju u aplikaciji
                var pushData = new Dictionary<string, string> {
                                         { "orderId", id.ToString() },
                                         { "screen", "OrderDetail" } // Primjer
                                     };
                if (buyer is null) return NoContent();
                // Pozovi servis za slanje PUSH notifikacije
                if (buyer.FcmDeviceToken is not null)
                    await _pushNotificationService.SendPushNotificationAsync(
                        buyer.FcmDeviceToken,
                        pushTitle,
                        pushBody,
                        pushData
                    );
                _logger.LogInformation("Push Notification task initiated for Buyer {BuyerId} for Order {OrderId} status update.", buyer.Id, id);
            }
            if (updateDto.StoreId is not null)
            {
                var seller = await _userManager.Users.FirstOrDefaultAsync(u => u.StoreId == updateDto.StoreId);
                await _notificationService.CreateNotificationAsync(
                       seller.Id,
                       $"Status Vaše narudžbe #{id} je ažuriran na '{status}'.",
                        id
                   );
                string notificationMessage = $"Status Vaše narudžbe #{id} je ažuriran na '{status}'.";
                string pushTitle = "Status Narudžbe Ažuriran";
                string pushBody = $"Status narudžbe #{id} je sada: {status}.";
                // Opcionalno: Dodaj podatke za navigaciju u aplikaciji
                if (seller is null) return NoContent();
                var pushData = new Dictionary<string, string> {
                                         { "orderId", id.ToString() },
                                         { "screen", "OrderDetail" } // Primjer
                                     };

                // Pozovi servis za slanje PUSH notifikacije
                if (seller.FcmDeviceToken is not null)
                    await _pushNotificationService.SendPushNotificationAsync(
                        seller.FcmDeviceToken,
                        pushTitle,
                        pushBody,
                        pushData
                    );
                _logger.LogInformation("Push Notification task initiated for Seller {BuyerId} for Order {OrderId} status update.", seller.Id, id);
            }
            return NoContent();
        }

        // PUT /api/admin/order/update/status/{id} --> Primarily for updating Status
        [HttpPut("order/update/status/{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] OrderUpdateStatusDto updateDto)
        {
            _logger.LogInformation("Attempting to update status for order ID: {OrderId} to {NewStatus}", id, updateDto.NewStatus);

            if (id <= 0)
            {
                _logger.LogWarning("UpdateOrderStatus request failed validation: Invalid ID {OrderId}", id);
                return BadRequest("Invalid Order ID provided.");
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Update order status request failed model validation for Order ID {OrderId}. Errors: {@ModelState}", id, ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }

            try
            {
                OrderStatus status = OrderStatus.Requested;
                if (updateDto.NewStatus is not null)
                {
                    if (Enum.TryParse(updateDto.NewStatus, true, out OrderStatus result))
                    {
                        status = result;
                    }
                    else
                    {
                        return BadRequest($"Nevalidan status {updateDto.NewStatus}");
                    }
                }

                var success = await _orderService.UpdateOrderStatusAsync(id, status);

                if (!success)
                {
                    // Could be NotFound or a concurrency issue, service layer logs details
                    _logger.LogWarning("Failed to update status for order ID: {OrderId}. Order might not exist or a concurrency issue occurred.", id);
                    // Check if the order actually exists to return a more specific error
                    var orderExists = await _orderService.GetOrderByIdAsync(id) != null;
                    if (!orderExists)
                    {
                        return NotFound($"Order with ID {id} not found.");
                    }
                    // If it exists, it might be a concurrency issue or other update failure
                    return BadRequest($"Failed to update status for order ID: {id}. See logs for details.");
                }



                _logger.LogInformation("Successfully updated status for order ID: {OrderId} to {NewStatus}", id, updateDto.NewStatus);
                return NoContent(); // Standard success response for PUT when no content is returned
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating status for order ID: {OrderId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred while updating the order status.");
            }
        }

        // DELETE /api/admin/order/{id}
        [HttpDelete("order/{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            _logger.LogInformation("Attempting to delete order with ID: {OrderId}", id);

            if (id <= 0)
            {
                _logger.LogWarning("DeleteOrder request failed validation: Invalid ID {OrderId}", id);
                return BadRequest("Invalid Order ID provided.");
            }

            try
            {
                var success = await _orderService.DeleteOrderAsync(id);

                if (!success)
                {
                    // Service logs if not found
                    _logger.LogWarning("Failed to delete order with ID: {OrderId}. Order might not exist.", id);
                    return NotFound($"Order with ID {id} not found."); // Return 404 if the delete operation indicated not found
                }

                _logger.LogInformation("Successfully deleted order with ID: {OrderId}", id);
                return NoContent(); // Standard success response for DELETE
            }
            catch (DbUpdateException dbEx) // Catch potential FK constraint issues if cascade delete isn't set up perfectly
            {
                _logger.LogError(dbEx, "Database error occurred while deleting order ID: {OrderId}. It might be referenced elsewhere.", id);
                // Consider returning 409 Conflict if it's due to dependencies
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while deleting order {id}. It might have related data preventing deletion.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while deleting order ID: {OrderId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred while deleting the order.");
            }
        }


        // Helper method to add errors to ModelState
        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
    }
}