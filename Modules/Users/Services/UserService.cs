using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Store.Models;
using Users.Interface;
using Users.Models;

namespace Users.Services
{
    public class UserService : IUserService
    {
        private readonly UserManager<User> _userManager;
        private readonly StoreDbContext _context;

        private readonly ILogger<UserService> _logger;

        public UserService(UserManager<User> userManager, StoreDbContext context, ILogger<UserService> logger)
        {
            _userManager = userManager;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves all users associated with a specific store.
        /// </summary>
        /// <param name="storeId">The ID of the store.</param>
        /// <returns>A collection of users associated with the store.</returns>
        public IEnumerable<User> GetUsersFromStore(int storeId)
        {
            // Validate if the store exists
            var store = _context.Stores.Find(storeId);
            if (store == null)
            {
                throw new KeyNotFoundException($"Store with ID {storeId} not found.");
            }

            // Retrieve users associated with the store using the StoreId foreign key
            return _userManager.Users
                .Where(u => u.StoreId == storeId)
                .ToList();
        }
        public async Task<User?> GetUserWithIdAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID {userId} not found.");
            }
            return user;
        }

        public async Task<string?> GetMyUsernameAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("GetMyUsernameAsync: Provided userId is null or empty.");
                return null;
            }
            _logger.LogInformation("Fetching username for current user ID: {UserId}", userId);
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("GetMyUsernameAsync: User with ID {UserId} not found.", userId);
            }
            return user?.UserName;
        }

        public async Task<string?> GetUsernameByIdAsync(string targetUserId)
        {
            if (string.IsNullOrEmpty(targetUserId))
            {
                _logger.LogWarning("GetUsernameByIdAsync: Provided targetUserId is null or empty.");
                return null;
            }
            _logger.LogInformation("Fetching username for target user ID: {TargetUserId}", targetUserId);
            var user = await _userManager.FindByIdAsync(targetUserId);
            if (user == null)
            {
                _logger.LogWarning("GetUsernameByIdAsync: User with ID {TargetUserId} not found.", targetUserId);
            }
            return user?.UserName;
        }

        public async Task<bool> UpdateMyUsernameAsync(string userId, UpdateUsernameDto dto)
        {
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("UpdateMyUsernameAsync: Provided userId is null or empty.");
                return false; // Ili baci ArgumentNullException
            }
            if (dto == null || string.IsNullOrWhiteSpace(dto.NewUsername))
            {
                _logger.LogWarning("UpdateMyUsernameAsync for User {UserId}: NewUsername is null or empty.", userId);
                throw new ArgumentException("New username cannot be empty.", nameof(dto.NewUsername));
            }

            _logger.LogInformation("User {UserId} attempting to update username to '{NewUsername}'", userId, dto.NewUsername);
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("UpdateMyUsernameAsync: User with ID {UserId} not found.", userId);
                return false;
            }

            if (user.UserName == dto.NewUsername)
            {
                _logger.LogInformation("User {UserId} username is already '{CurrentUsername}'. No update needed.", userId, user.UserName);
                return true;
            }

            var setResult = await _userManager.SetUserNameAsync(user, dto.NewUsername);
            if (!setResult.Succeeded)
            {
                _logger.LogError("Failed to set UserName for User {UserId}. Errors: {Errors}", userId, string.Join(", ", setResult.Errors.Select(e => e.Description)));
                return false;
            }

            var updateResult = await _userManager.UpdateAsync(user);
            if (updateResult.Succeeded)
            {
                _logger.LogInformation("Successfully updated username for User {UserId} to '{NewUsername}'", userId, dto.NewUsername);
                return true;
            }
            else
            {
                _logger.LogError("Failed to update user after setting username for User {UserId}. Errors: {Errors}", userId, string.Join(", ", updateResult.Errors.Select(e => e.Description)));

                return false;
            }
        }
    }
}