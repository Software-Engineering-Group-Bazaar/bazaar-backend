using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Store.Interface;
using Store.Models;
using Store.Services;

namespace Store.Services
{
    public class StoreCategoryService : IStoreCategoryService
    {
        private readonly StoreDbContext _context;

        public StoreCategoryService(StoreDbContext context)
        {
            _context = context;
        }

        // Create a new category
        public StoreCategory CreateCategory(string name)
        {
            var category = new StoreCategory
            {
                name = name,
                stores = new List<StoreModel>()
            };

            _context.StoreCategories.Add(category);
            _context.SaveChanges();
            return category;
        }

        // Get all categories
        public IEnumerable<StoreCategory> GetAllCategories()
        {
            return _context.StoreCategories.ToList();
        }

        // Get a category by ID
        public StoreCategory? GetCategoryById(int id)
        {
            return _context.StoreCategories.Include(c => c.stores).FirstOrDefault(c => c.id == id);
        }

        // Update a category
        public StoreCategory? UpdateCategory(int id, string name)
        {
            var category = _context.StoreCategories.Find(id);
            if (category == null)
            {
                return null;
            }

            category.name = name;

            _context.SaveChanges();
            return category;
        }

        // Delete a category
        public bool DeleteCategory(int id)
        {
            var category = _context.StoreCategories.Find(id);
            if (category == null)
            {
                return false;
            }

            _context.StoreCategories.Remove(category);
            _context.SaveChanges();
            return true;
        }

        // Get all stores in a category
        public IEnumerable<StoreModel> GetStoresInCategory(int categoryId)
        {
            var category = _context.StoreCategories
                .Include(c => c.stores) // Ensure stores are loaded
                .FirstOrDefault(c => c.id == categoryId);

            return category?.stores ?? new List<StoreModel>();
        }
    }
}