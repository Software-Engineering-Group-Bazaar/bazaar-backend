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

        public UserService(UserManager<User> userManager, StoreDbContext context)
        {
            _userManager = userManager;
            _context = context;
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
    }
}