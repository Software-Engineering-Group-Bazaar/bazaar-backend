using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Users.Interface;
using Users.Interfaces;
using Users.Models;
using Users.Models.Dtos;

namespace Users.Controllers
{
    [ApiController]
    [Route("api/user-profile")]
    [Authorize]
    public class UserProfileController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UserProfileController> _logger;
        private readonly IAddressService _addressService;
        public UserProfileController(IUserService userService,
            ILogger<UserProfileController> logger,
            IAddressService addressService
        )
        {
            _userService = userService;
            _logger = logger;
            _addressService = addressService;
        }

        // GET /api/user-profile/my-username
        [HttpGet("my-username")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<string>> GetMyUsername()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User ID not found in token.");
            }

            _logger.LogInformation("User {UserId} requesting their username.", userId);
            var username = await _userService.GetMyUsernameAsync(userId);

            if (username == null)
            {
                _logger.LogWarning("Username not found for User {UserId}.", userId);
                return NotFound("Username not found for the current user.");
            }
            return Ok(username);
        }

        // GET /api/user-profile/{targetUserId}/username
        [HttpGet("{targetUserId}/username")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<string>> GetUsernameById(string targetUserId)
        {
            var requestingUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            _logger.LogInformation("User {RequestingUserId} requesting username for TargetUser {TargetUserId}.", requestingUserId, targetUserId);

            if (string.IsNullOrEmpty(targetUserId))
            {
                return BadRequest("Target User ID cannot be empty.");
            }

            var username = await _userService.GetUsernameByIdAsync(targetUserId);

            if (username == null)
            {
                _logger.LogWarning("Username not found for TargetUser {TargetUserId}.", targetUserId);
                return NotFound($"Username not found for user ID {targetUserId}.");
            }
            return Ok(username);
        }

        [HttpPut("my-username")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateMyUsername([FromBody] UpdateUsernameDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User ID not found in token.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _logger.LogInformation("User {UserId} attempting to update their username to '{NewUsername}'.", userId, dto.NewUsername);

            try
            {
                var success = await _userService.UpdateMyUsernameAsync(userId, dto);
                if (!success)
                {
                    _logger.LogWarning("Failed to update username for User {UserId}. User not found or update operation failed.", userId);
                    return NotFound("User not found or update failed. Check logs for details.");
                }
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Argument error while User {UserId} updating username.", userId);
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation while User {UserId} updating username.", userId);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while User {UserId} updating username.", userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating username.");
            }
        }

        [HttpGet("address")]
        [ProducesResponseType(typeof(ICollection<AddressDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUserAddresses()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("[StoresController] CreateStore - Could not find user ID claim for the authenticated user.");
                return Unauthorized("User ID claim not found."); // 401 Unauthorized
            }
            _logger?.LogInformation("Attempting to get addresses for UserId: {UserId}", userId);

            var addresses = await _addressService.GetUserAddressesAsync(userId);

            if (addresses == null) // Iako GetUserAddressesAsync vjerojatno vraća praznu listu, ne null
            {
                _logger?.LogWarning("[AddressController] GetUserAddresses - Address service returned null for UserId: {UserId}", userId);
                return Ok(new List<AddressDto>()); // Vratiti praznu listu ako je to prikladno
            }

            var addressDtos = addresses.Select(address => new AddressDto
            {
                Id = address.Id,
                Address = address.StreetAddress,
                Latitude = address.Latitude,
                Longitude = address.Longitude,
                UserId = address.UserId
            }).ToList();

            return Ok(addressDtos);
        }

        [HttpGet("address/{id}")]
        [ProducesResponseType(typeof(AddressDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAddressById(int id)
        {
            _logger?.LogInformation("Attempting to get address with Id: {Id}", id);

            var address = await _addressService.GetAddressByIdAsync(id);

            if (address == null)
            {
                return Ok(null);
            }

            return Ok(new AddressDto
            {
                Id = address.Id,
                Address = address.StreetAddress,
                Latitude = address.Latitude,
                Longitude = address.Longitude,
                UserId = address.UserId
            });
        }


        [HttpPost("address")]
        [ProducesResponseType(typeof(AddressDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateAddress([FromBody] AddressDto address)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("[StoresController] CreateStore - Could not find user ID claim for the authenticated user.");
                return Unauthorized("User ID claim not found."); // 401 Unauthorized
            }

            if (address == null)
            {
                return BadRequest("Address data is null.");
            }

            // Osnovna validacija modela (npr. ako koristite DataAnnotations)
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrEmpty(address.UserId))
            {
                address.UserId = userId;
            }

            _logger?.LogInformation("Attempting to create address for UserId: {UserId}", address.UserId);
            try
            {
                await _addressService.CreateAddressAsync(new Address
                {
                    Id = address.Id,
                    StreetAddress = address.Address,
                    Latitude = address.Latitude,
                    Longitude = address.Longitude,
                    UserId = address.UserId
                });
                // Vraća 201 Created s lokacijom nove adrese i samim objektom adrese
                // Pretpostavljamo da će CreateAddressAsync popuniti address.Id nakon spremanja
                return CreatedAtAction(nameof(GetAddressById), new { id = address.Id }, address);
            }
            catch (InvalidDataException ex)
            {
                _logger?.LogWarning(ex, "Invalid data while creating address for UserId: {UserId}", address.UserId);
                return BadRequest(ex.Message);
            }
            catch (Exception ex) // Hvatanje općenitih grešaka
            {
                _logger?.LogError(ex, "An unexpected error occurred while creating address for UserId: {UserId}", address.UserId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        [HttpDelete("address/{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAddress(int id)
        {
            _logger?.LogInformation("Attempting to delete address with Id: {AddressId}", id);
            try
            {
                await _addressService.DeleteAddressByIdAsync(id);
                return NoContent(); // 204 No Content je standardni odgovor za uspješan DELETE
            }
            catch (KeyNotFoundException ex)
            {
                _logger?.LogWarning(ex, "Address with Id: {AddressId} not found for deletion.", id);
                return NotFound(ex.Message);
            }
            catch (Exception ex) // Hvatanje općenitih grešaka
            {
                _logger?.LogError(ex, "An unexpected error occurred while deleting address with Id: {AddressId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }
    }
}