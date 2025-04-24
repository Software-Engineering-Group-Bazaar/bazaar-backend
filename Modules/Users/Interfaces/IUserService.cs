using System.Collections.Generic;
using Users.Models;

namespace Users.Interface
{
    public interface IUserService
    {
        /// <summary>
        /// Retrieves all users associated with a specific store.
        /// </summary>
        /// <param name=""storeId"">The ID of the store.</param>
        /// <returns>A collection of users associated with the store.</returns>
        IEnumerable<User> GetUsersFromStore(int storeId);

        Task<User?> GetUserWithIdAsync(string userId);
    }
}