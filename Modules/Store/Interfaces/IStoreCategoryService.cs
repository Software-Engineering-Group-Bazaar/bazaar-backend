using System;
using System.Collections.Generic;
using Store.Models;

namespace Store.Services
{
    public interface IStoreCategoryService
    {
        // Create a new category
        StoreCategory CreateCategory(string name);

        // Get all categories
        IEnumerable<StoreCategory> GetAllCategories();

        // Get a category by ID
        StoreCategory? GetCategoryById(Guid id);

        // Update a category
        StoreCategory? UpdateCategory(Guid id, string name);

        // Delete a category
        bool DeleteCategory(Guid id);

        // Get all stores in a category
        IEnumerable<StoreModel> GetStoresInCategory(Guid categoryId);
    }
}