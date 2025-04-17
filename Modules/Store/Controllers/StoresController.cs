using System;
using System.Collections.Generic;
using System.Linq; // Potrebno za SelectMany u logovanju ModelState grešaka
using System.Security.Claims;
using System.Threading.Tasks;
using AdminApi.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity; // Za UserManager
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Store.Interface;
using Store.Models;
using Users.Models; // Za User model

namespace Store.Controllers
{
    [ApiController]
    [Route("api/store")] // Osnovna putanja /api/store
    public class StoresController : ControllerBase
    {
        private readonly IStoreService _storeService;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<StoresController> _logger;

        // Izbacujemo IStoreCategoryService jer se kategorije sada nalaze u svom kontroleru
        public StoresController(IStoreService storeService, UserManager<User> userManager, ILogger<StoresController> logger)
        {
            _storeService = storeService ?? throw new ArgumentNullException(nameof(storeService));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET /api/store - Dohvati sve prodavnice
        [HttpGet]
        [AllowAnonymous] // Svi mogu vidjeti listu prodavnica
        [ProducesResponseType(typeof(IEnumerable<StoreGetDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllStores() // async Task<IActionResult>
        {
            _logger.LogInformation("[StoresController] Attempting to retrieve all stores.");
            try
            {
                var stores = await _storeService.GetAllStoresAsync(); // Pozovi async metodu
                return Ok(stores); // Servis vraća DTO listu
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[StoresController] Error retrieving all stores.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving stores.");
            }
        }

        // GET /api/store/{id} - Dohvati specifičnu prodavnicu
        [HttpGet("{id:int}")]
        [AllowAnonymous] // Svi mogu vidjeti detalje prodavnice
        [ProducesResponseType(typeof(StoreGetDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetStoreById(int id) // async Task<IActionResult>
        {
            if (id <= 0) return BadRequest("Invalid store ID.");
            _logger.LogInformation("[StoresController] Attempting to retrieve store with ID {StoreId}", id);
            try
            {
                var store = await _storeService.GetStoreByIdAsync(id); // Pozovi async metodu
                if (store == null)
                {
                    _logger.LogWarning("[StoresController] Store with ID {StoreId} not found.", id);
                    return NotFound($"Store with ID {id} not found.");
                }
                return Ok(store); // Servis vraća DTO
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[StoresController] Error retrieving store with ID {StoreId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the store.");
            }
        }

        // GET /api/store/my-store - Dohvati prodavnicu ulogovanog Sellera
        [HttpGet("my-store")]
        [Authorize(Roles = "Seller")] // Samo za Sellera
        [ProducesResponseType(typeof(StoreGetDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<StoreGetDto>> GetMyStore() // async Task<ActionResult<...>>
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            _logger.LogInformation("[StoresController] Attempting to retrieve store for User {UserId}", userId);
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null) return StatusCode(StatusCodes.Status500InternalServerError, "Authenticated user could not be found.");
                if (!user.StoreId.HasValue) return NotFound($"No store associated with the current user.");

                // Pozovi async metodu servisa
                var storeDto = await _storeService.GetStoreByIdAsync(user.StoreId.Value);
                if (storeDto == null)
                {
                    _logger.LogError("[StoresController] Data inconsistency: User {UserId} has StoreId {StoreId}, but store not found.", userId, user.StoreId.Value);
                    // Vrati NotFound umjesto 500 da bude jasnije
                    return NotFound($"The store (ID: {user.StoreId.Value}) associated with the user was not found.");
                }

                _logger.LogInformation("[StoresController] Successfully retrieved store {StoreId} for User {UserId}", storeDto.Id, userId);
                return Ok(storeDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[StoresController] Error retrieving store for User {UserId}.", userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while retrieving your store.");
            }
        }

        // POST /api/store - Kreiraj prodavnicu (samo za Sellera za sebe)
        [HttpPost]
        [Authorize(Roles = "Seller")] // ➤ Sada SAMO Seller može pozvati ovaj endpoint
        [ProducesResponseType(typeof(StoreGetDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)] // Ako nema Seller rolu ili već ima store
        [ProducesResponseType(StatusCodes.Status409Conflict)] // Ako user već ima store (iz servisa)
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateStore([FromBody] StoreCreateDto createDto) // StoreCreateDto sada NEMA TargetSellerUserId
        {
            // Validacija DTO-a preko [ApiController] i ModelState
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // Dobij ID ulogovanog korisnika (Sellera)
            var sellerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(sellerUserId))
            {
                // Iako [Authorize] ovo pokriva, dodatna provjera
                _logger.LogWarning("User ID claim not found for an authenticated user trying to create a store.");
                return Unauthorized("User identifier not found.");
            }

            _logger.LogInformation("[StoresController] Seller {SellerUserId} attempting to create a store for themselves.", sellerUserId);

            try
            {
                // Pozovi servisnu metodu koja prima ID Sellera i DTO
                var createdStoreDto = await _storeService.CreateStoreForSellerAsync(sellerUserId, createDto);

                // Servis baca izuzetke za greške (npr. već ima store, kategorija ne postoji...)
                if (createdStoreDto == null) // Neočekivano ako servis baca izuzetke
                {
                    _logger.LogError("CreateStoreForSellerAsync returned null unexpectedly for seller {SellerUserId}.", sellerUserId);
                    return StatusCode(StatusCodes.Status500InternalServerError, "Store creation failed unexpectedly.");
                }

                // Vrati lokaciju nove prodavnice
                return CreatedAtAction(nameof(GetStoreById), new { id = createdStoreDto.Id }, createdStoreDto);
            }
            catch (InvalidOperationException ex) // Npr. User already has store
            {
                _logger.LogWarning("Conflict creating store for user {SellerUserId}: {Message}", sellerUserId, ex.Message);
                return Conflict(new { message = ex.Message }); // 409 Conflict
            }
            catch (ArgumentException ex) // Npr. Category not found
            {
                _logger.LogWarning("Bad request creating store for user {SellerUserId}: {Message}", sellerUserId, ex.Message);
                return BadRequest(new { message = ex.Message }); // 400 Bad Request
            }
            catch (KeyNotFoundException ex) // Npr. User not found iz servisa
            {
                _logger.LogError(ex, "User not found exception during store creation for user ID {SellerUserId}", sellerUserId);
                return StatusCode(StatusCodes.Status500InternalServerError, "Data consistency error during store creation.");
            }
            catch (Exception ex) // Sve ostale greške
            {
                _logger.LogError(ex, "Error creating store for user {SellerUserId}.", sellerUserId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while creating the store.");
            }
        }

        // PUT /api/store/{id} - Ažuriraj prodavnicu
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Seller,Admin")]
        [ProducesResponseType(typeof(StoreGetDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateStore(int id, [FromBody] StoreUpdateDto updateDto) // Koristi Update DTO
        {
            if (id <= 0) return BadRequest("Invalid store ID.");
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User identifier not found.");

            try
            {
                // Pozovi async metodu
                var updatedStoreDto = await _storeService.UpdateStoreAsync(id, updateDto, userId, isAdmin);

                if (updatedStoreDto == null) return NotFound($"Store with ID {id} not found.");

                return Ok(updatedStoreDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Forbidden attempt by User {UserId} to update Store {StoreId}", userId, id);
                return Forbid();
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Bad request updating store {StoreId}: {Message}", id, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating store {StoreId} by user {UserId} (IsAdmin: {IsAdmin}).", id, userId, isAdmin);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the store.");
            }
        }

        // DELETE /api/store/{id} - Briši prodavnicu
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")] // Samo Admin briše
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteStore(int id) // async Task<IActionResult>
        {
            if (id <= 0) return BadRequest("Invalid store ID.");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = true;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            try
            {
                // Pozovi async metodu
                var success = await _storeService.DeleteStoreAsync(id, userId, isAdmin);
                if (!success) return NotFound($"Store with ID {id} not found.");
                return NoContent();
            }
            catch (InvalidOperationException ex) // Uhvati grešku iz servisa ako se ne može obrisati
            {
                _logger.LogWarning(ex, "Conflict deleting store {StoreId} requested by Admin {UserId}", id, userId);
                return Conflict(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex) // Ne bi se smjelo desiti
            {
                _logger.LogWarning(ex, "Auth error deleting store {StoreId} by Admin {UserId}", id, userId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting store {StoreId} by Admin {UserId}.", id, userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the store.");
            }
        }

        // Nema više metoda za kategorije ovdje
    }
}