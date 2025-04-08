using System;
using System.Collections.Generic;
using Store.Models;

namespace Store.Services
{
    public interface IStoreService
    {
        // Create a new store
        StoreModel CreateStore(string name, Guid categoryId, string address, string description);

        // Get all stores
        IEnumerable<StoreModel> GetAllStores();

        // Get a store by ID
        StoreModel? GetStoreById(Guid id);

        // Update a store
        StoreModel? UpdateStore(Guid id, string name, Guid categoryId, string address, string description, bool isActive);

        // Delete a store
        bool DeleteStore(Guid id);
    }
}