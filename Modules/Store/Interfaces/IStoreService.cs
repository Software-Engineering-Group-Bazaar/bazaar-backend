using System;
using System.Collections.Generic;
using Store.Models;

namespace Store.Interface
{
    public interface IStoreService //TODO: Make async methods
    {
        // Create a new store
        StoreModel CreateStore(string name, int categoryId, string address, string description);

        // Get all stores
        IEnumerable<StoreModel> GetAllStores();

        // Get a store by ID
        StoreModel? GetStoreById(int id);

        // Update a store
        StoreModel? UpdateStore(int id, string? name, int? categoryId, string? address, string? description, bool? isActive);

        // Delete a store
        bool DeleteStore(int id);
        Task<bool> DeleteStoreAsync(int id);
    }
}