using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Store.Interface;
using Store.Models;
using Store.Services;

namespace Store.Services
{
    public class StoreService : IStoreService
    {
        private readonly StoreDbContext _context;

        public StoreService(StoreDbContext context)
        {
            _context = context;
        }

        // Create a new store
        public StoreModel CreateStore(string _name, int _categoryId, string _address, string _description)
        {
            var _category = _context.StoreCategories.Find(_categoryId);
            if (_category == null)
            {
                throw new ArgumentException("Category not found.");
            }

            var store = new StoreModel
            {
                name = _name,
                category = _category,
                address = _address,
                description = _description,
                isActive = true // Default value for new stores
            };

            _context.Stores.Add(store);
            _context.SaveChanges();
            return store;
        }

        // Get all stores
        public IEnumerable<StoreModel> GetAllStores()
        {
            return _context.Stores.Include(s => s.category).ToList();
        }

        // Get a store by ID
        public StoreModel? GetStoreById(int id)
        {
            return _context.Stores.Include(s => s.category).FirstOrDefault(s => s.id == id);
        }

        // Update a store
        public StoreModel? UpdateStore(int id, string name, int categoryId, string address, string description, bool isActive)
        {
            var store = _context.Stores.Find(id);
            if (store == null)
            {
                return null;
            }

            var category = _context.StoreCategories.Find(categoryId);
            if (category == null)
            {
                throw new ArgumentException("Category not found.");
            }

            store.name = name;
            store.category = category;
            store.address = address;
            store.description = description;
            store.isActive = isActive;

            _context.SaveChanges();
            return store;
        }

        // Delete a store
        public bool DeleteStore(int id)
        {
            var store = _context.Stores.Find(id);
            if (store == null)
            {
                return false;
            }

            _context.Stores.Remove(store);
            _context.SaveChanges();
            return true;
        }

        // Check if a store exists by ID
        public bool DoesStoreExist(int id)
        {
            return _context.Stores.Any(s => s.id == id);
        }
    }
}