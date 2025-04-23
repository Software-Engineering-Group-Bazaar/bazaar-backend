using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Inventory.Dtos;
using Inventory.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Inventory.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryService _inventoryService;
        private readonly ILogger<InventoryController> _logger;

        public InventoryController(IInventoryService inventoryService, ILogger<InventoryController> logger)
        {
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // POST /api/Inventory/create
        [HttpPost("create")]
        [Authorize(Roles = "Admin, Seller")]
        [ProducesResponseType(typeof(InventoryDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)] // Za validacione greške ili loše argumente
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)] // Ako Seller pokuša za tuđi store (ili nije Seller/Admin)
        [ProducesResponseType(StatusCodes.Status404NotFound)] // Ako ProductId ili StoreId ne postoje
        [ProducesResponseType(StatusCodes.Status409Conflict)] // Ako zapis već postoji
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<InventoryDto>> CreateInventoryRecord([FromBody] CreateInventoryRequestDto createDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            bool isAdmin = User.IsInRole("Admin");

            _logger.LogInformation("API: User {UserId} (IsAdmin: {IsAdmin}) attempting to create inventory.", userId, isAdmin);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("API: Create inventory request failed model validation for User {UserId}. Errors: {@ModelState}", userId, ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }

            try
            {
                var createdInventoryDto = await _inventoryService.CreateInventoryAsync(userId, isAdmin, createDto);

                if (createdInventoryDto == null)
                {
                    _logger.LogError("API: Inventory creation failed for User {UserId}, service returned null.", userId);
                    return StatusCode(StatusCodes.Status500InternalServerError, "Inventory creation failed unexpectedly.");
                }

                _logger.LogInformation("API: Successfully created inventory record ID {InventoryId} for User {UserId}.", createdInventoryDto.Id, userId);
                return StatusCode(StatusCodes.Status201Created, createdInventoryDto);
            }
            catch (ArgumentNullException ex) { _logger.LogWarning(ex, "API: Null argument during inventory creation by User {UserId}.", userId); return BadRequest(new { message = ex.Message }); }
            catch (ArgumentException ex) { _logger.LogWarning(ex, "API: Invalid argument during inventory creation by User {UserId}.", userId); return BadRequest(new { message = ex.Message }); }
            catch (KeyNotFoundException ex) { _logger.LogWarning(ex, "API: Entity not found during inventory creation by User {UserId}.", userId); return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { _logger.LogWarning(ex, "API: Unauthorized inventory creation attempt by User {UserId}.", userId); return Forbid(); }
            catch (InvalidOperationException ex) { _logger.LogWarning(ex, "API: Invalid operation (e.g., duplicate) during inventory creation by User {UserId}.", userId); return Conflict(new { message = ex.Message }); } // 409 Conflict za duplikat
            catch (DbUpdateException ex) { _logger.LogError(ex, "API: Database error during inventory creation by User {UserId}.", userId); return StatusCode(StatusCodes.Status500InternalServerError, "Database error during creation."); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API: Unexpected error during inventory creation by User {UserId}.", userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // GET /api/Inventory
        [HttpGet]
        [Authorize(Roles = "Admin, Seller")]
        [ProducesResponseType(typeof(IEnumerable<InventoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)] // Ako su filteri nevalidni (npr. negativni ID)
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)] // Ako korisnik nema pravu rolu
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<InventoryDto>>> GetInventory(
            [FromQuery] int? productId = null,
            [FromQuery] int? storeId = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            bool isAdmin = User.IsInRole("Admin");

            _logger.LogInformation("API: User {UserId} (IsAdmin: {IsAdmin}) getting inventory. Filters - ProductId: {ProductId}, StoreId: {StoreId}",
                                   userId, isAdmin, productId?.ToString() ?? "N/A", storeId?.ToString() ?? "N/A");

            if (productId.HasValue && productId.Value <= 0)
            {
                _logger.LogWarning("API: Get inventory request failed validation: Invalid ProductId {ProductId}", productId.Value);
                return BadRequest("Invalid Product ID provided.");
            }
            if (storeId.HasValue && storeId.Value <= 0)
            {
                _logger.LogWarning("API: Get inventory request failed validation: Invalid StoreId {StoreId}", storeId.Value);
                return BadRequest("Invalid Store ID provided.");
            }

            try
            {
                var inventoryDtos = await _inventoryService.GetInventoryAsync(userId, isAdmin, productId, storeId);

                _logger.LogInformation("API: Successfully retrieved {Count} inventory records for User {UserId}.", inventoryDtos.Count(), userId);
                return Ok(inventoryDtos);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "API: Unauthorized inventory access attempt by User {UserId}.", userId);
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API: Error retrieving inventory for User {UserId}.", userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving inventory data.");
            }
        }

        // PUT /api/Inventory/update/quantity
        [HttpPut("update/quantity")]
        [Authorize(Roles = "Admin, Seller")]
        [ProducesResponseType(typeof(InventoryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)] // Ako inventory zapis ne postoji
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<InventoryDto>> UpdateInventoryQuantity([FromBody] UpdateInventoryQuantityRequestDto updateDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            bool isAdmin = User.IsInRole("Admin");

            _logger.LogInformation("API: User {UserId} (IsAdmin: {IsAdmin}) attempting to update inventory quantity.", userId, isAdmin);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("API: Update inventory quantity request failed model validation for User {UserId}. Errors: {@ModelState}", userId, ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }
            try
            {
                var updatedInventoryDto = await _inventoryService.UpdateQuantityAsync(userId, isAdmin, updateDto);

                if (updatedInventoryDto == null)
                {
                    _logger.LogWarning("API: Inventory record not found or update failed (concurrency?) for ProductId {ProductId}, StoreId {StoreId}, requested by User {UserId}.",
                                       updateDto.ProductId, updateDto.StoreId, userId);
                    return NotFound($"Inventory record not found for Product ID {updateDto.ProductId} in Store ID {updateDto.StoreId}.");
                }

                _logger.LogInformation("API: Successfully updated inventory quantity for record ID {InventoryId}, requested by User {UserId}.", updatedInventoryDto.Id, userId);
                return Ok(updatedInventoryDto);

            }
            catch (ArgumentNullException ex) { _logger.LogWarning(ex, "API: Null argument during inventory update by User {UserId}.", userId); return BadRequest(new { message = ex.Message }); }
            catch (ArgumentException ex) { _logger.LogWarning(ex, "API: Invalid argument during inventory update by User {UserId}.", userId); return BadRequest(new { message = ex.Message }); }
            catch (KeyNotFoundException ex) { _logger.LogWarning(ex, "API: Entity not found during inventory update by User {UserId}.", userId); return NotFound(new { message = ex.Message }); }
            catch (UnauthorizedAccessException ex) { _logger.LogWarning(ex, "API: Unauthorized inventory update attempt by User {UserId}.", userId); return Forbid(ex.Message); }
            catch (DbUpdateException ex) { _logger.LogError(ex, "API: Database error during inventory update by User {UserId}.", userId); return StatusCode(StatusCodes.Status500InternalServerError, "Database error during update."); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API: Unexpected error during inventory update by User {UserId}.", userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // DELETE /api/Inventory/{id}
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin, Seller")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)] // Nevalidan ID
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)] // Seller pokušava obrisati tuđi
        [ProducesResponseType(StatusCodes.Status404NotFound)] // Zapis ne postoji
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteInventoryRecord(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            bool isAdmin = User.IsInRole("Admin");

            _logger.LogInformation("API: User {UserId} (IsAdmin: {IsAdmin}) attempting to delete inventory record ID {InventoryId}.", userId, isAdmin, id);

            // Osnovna validacija ID-a
            if (id <= 0)
            {
                _logger.LogWarning("API: Delete inventory request failed validation: Invalid InventoryId {InventoryId}", id);
                return BadRequest("Invalid Inventory ID provided.");
            }

            try
            {
                var success = await _inventoryService.DeleteInventoryAsync(userId, isAdmin, id);

                if (!success)
                {
                    _logger.LogWarning("API: Inventory record ID {InventoryId} not found for deletion by User {UserId}.", id, userId);
                    return NotFound($"Inventory record with ID {id} not found.");
                }

                _logger.LogInformation("API: Successfully deleted inventory record ID {InventoryId} by User {UserId}.", id, userId);
                return NoContent();

            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "API: Unauthorized inventory deletion attempt for ID {InventoryId} by User {UserId}.", id, userId);
                return Forbid("You are not authorized to delete this inventory record.");
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "API: Invalid argument during inventory deletion for ID {InventoryId} by User {UserId}.", id, userId);
                return BadRequest(new { message = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "API: Database error during inventory deletion for ID {InventoryId} by User {UserId}.", id, userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "Database error during deletion.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API: Unexpected error during inventory deletion for ID {InventoryId} by User {UserId}.", id, userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }
    }
}