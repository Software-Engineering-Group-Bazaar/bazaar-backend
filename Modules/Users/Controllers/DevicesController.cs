using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Users.Dtos;
using Users.Interfaces;
using Users.Models;
using Users.Models.Dtos;

namespace Users.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class DevicesController : ControllerBase
    {

        private readonly UserManager<User> _userManager;

        public DevicesController(UserManager<User> userManager)
        {
            _userManager = userManager;
        }

        [HttpPost("me/device")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RegisterDevice([FromBody] DeviceRegistrationDto registrationDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID not found.");

            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null) return NotFound("User not found.");

                if (user.FcmDeviceToken != registrationDto.DeviceToken)
                {
                    user.FcmDeviceToken = registrationDto.DeviceToken;
                    var result = await _userManager.UpdateAsync(user);

                    if (!result.Succeeded)
                    {
                        return StatusCode(StatusCodes.Status500InternalServerError, "Failed to update device registration.");
                    }
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred: {ex.Message}");
            }
        }
    }
}