using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdminApi.DTOs; // Your DTOs namespace (Ensure this namespace is correct)
using Catalog.DTO;
using Catalog.Models;
using Catalog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging; // Required for logging
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

        public AdminController(
            UserManager<User> userManager,
            RoleManager<IdentityRole> roleManager,
            IProductService productService,
            IStoreService storeService,
            IStoreCategoryService storeCategoryService,
            IProductCategoryService productCategoryService,
            ILogger<AdminController> logger) // Add logger to constructor
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _productService = productService;
            _storeService = storeService;
            _productCategoryService = productCategoryService;
            _storeCategoryService = storeCategoryService;
            _logger = logger; // Assign injected logger
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
                var createdProductDto = new ProductGetDto
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
        [ProducesResponseType(typeof(IEnumerable<StoreGetDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<IEnumerable<StoreGetDto>> GetStores()
        {
            _logger.LogInformation("Attempting to retrieve all stores.");

            try
            {
                var stores = _storeService.GetAllStores(); // kasnije prebaciti u asinhrono
                if (stores == null || !stores.Any())
                {
                    _logger.LogInformation("No stores found.");
                    return Ok(new List<StoreGetDto>());
                }

                var storeDtos = stores.Select(store => new StoreGetDto
                {
                    Id = store.id,
                    Name = store.name,
                    Address = store.address,
                    Description = store.description,
                    IsActive = store.isActive,
                    CategoryName = store.category.name
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
        [ProducesResponseType(typeof(IEnumerable<StoreGetDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<IEnumerable<StoreGetDto>> GetStoresById(int id)
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

                var storeDto = new StoreGetDto
                {
                    Id = id,
                    Address = stores.address,
                    CategoryName = stores.category.name,
                    IsActive = stores.isActive,
                    Description = stores.description,
                    Name = stores.name
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
        [ProducesResponseType(typeof(StoreGetDto), StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<StoreGetDto> UpdateStore([FromBody] StoreUpdateDto dto)
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
        [ProducesResponseType(typeof(StoreGetDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<StoreGetDto> CreateStore([FromBody] StoreCreateDto dto)
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
                WholesalePrice = product.WholesalePrice,
                Weight = product.Weight,
                WeightUnit = product.WeightUnit,
                Volume = product.Volume,
                VolumeUnit = product.VolumeUnit,
                StoreId = product.StoreId,
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
                WholesalePrice = product.WholesalePrice,
                Weight = product.Weight,
                WeightUnit = product.WeightUnit,
                Volume = product.Volume,
                VolumeUnit = product.VolumeUnit,
                StoreId = product.StoreId,
                Photos = product.Pictures.Select(photo => photo.Url).ToList()
            };

            return Ok(productDto);
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