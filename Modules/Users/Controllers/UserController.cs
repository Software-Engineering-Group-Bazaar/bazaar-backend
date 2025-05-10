using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Users.Interface;
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

        public UserProfileController(IUserService userService, ILogger<UserProfileController> logger)
        {
            _userService = userService;
            _logger = logger;
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
    }
}