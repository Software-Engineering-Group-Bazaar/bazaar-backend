using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdminApi.DTOs; // Your DTOs namespace (Ensure this namespace is correct)
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging; // Required for logging
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

        public AdminController(
            UserManager<User> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<AdminController> logger) // Add logger to constructor
        {
            _userManager = userManager;
            _roleManager = roleManager;
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
            var roleResult = await _userManager.AddToRoleAsync(user, Role.Seller.ToString());
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