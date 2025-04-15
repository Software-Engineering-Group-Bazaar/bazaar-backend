using System;
using System.Collections.Generic;
using Store.Models;

namespace Store.Interface
{
    public interface IStoreCategoryService
    {
        // Create a new category
        StoreCategory CreateCategory(string name);

        // Get all categories
        IEnumerable<StoreCategory> GetAllCategories();

        // Get a category by ID
        StoreCategory? GetCategoryById(int id);

        // Update a category
        StoreCategory? UpdateCategory(int id, string name);

        // Delete a category
        bool DeleteCategory(int id);

        // Get all stores in a category
        IEnumerable<StoreModel> GetStoresInCategory(int categoryId);
    }
}