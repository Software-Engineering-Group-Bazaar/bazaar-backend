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
        private readonly IServiceScopeFactory _scopeFactory;

        public StoreService(StoreDbContext context, IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _scopeFactory = scopeFactory;
        }

        // Create a new store
        public StoreModel CreateStore(string _name, int _categoryId, string _address, string _description, int placeId)
        {
            var _category = _context.StoreCategories.Find(_categoryId);
            var _place = _context.Places.Find(placeId);
            if (_category == null)
            {
                throw new ArgumentException("Category not found.");
            }
            if (_place == null)
            {
                throw new ArgumentException("Category not found.");
            }

            var store = new StoreModel
            {
                name = _name,
                category = _category,
                address = _address,
                description = _description,
                place = _place,
                isActive = true // Default value for new stores
            };

            _context.Stores.Add(store);
            _context.SaveChanges();
            return store;
        }

        // Get all stores
        public IEnumerable<StoreModel> GetAllStores()
        {
            return _context.Stores.Include(s => s.category).Include(s => s.place)
                .Include(s => s.place.Region).ToList();
        }

        // Get a store by ID
        public StoreModel? GetStoreById(int id)
        {
            return _context.Stores.Include(s => s.category).Include(s => s.place)
                .Include(s => s.place.Region).FirstOrDefault(s => s.id == id);
        }

        // Update a store
        public StoreModel? UpdateStore(int id, string? name, int? categoryId, string? address, string? description, bool? isActive)
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
            if (name is not null)
                store.name = name;
            if (category is not null)
                store.category = category;
            // if (address is not null)
            //     store.address = address;
            if (description is not null)
                store.description = description;
            if (isActive is not null)
                store.isActive = (bool)isActive;

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

        public async Task<bool> DeleteStoreAsync(int id)
        {
            var store = await _context.Stores.FindAsync(id);
            if (store == null)
            {
                return false;
            }

            _context.Stores.Remove(store);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<StoreModel>> SearchStoresAsync(string query)
        {

            if (string.IsNullOrWhiteSpace(query))
            {
                return await _context.Stores.Include(s => s.category).Include(s => s.place)
                .Include(s => s.place.Region).ToListAsync();
            }

            var normalizedSearchTerm = query.Trim().ToLower();

            var stores = await _context.Stores
                .Include(s => s.category)
                .Include(s => s.place)
                .Include(s => s.place.Region)
                .Where(s => s.name.ToLower().Contains(normalizedSearchTerm))
                .ToListAsync();

            return stores;
        }

        public async Task<IEnumerable<StoreModel>> GetAllStoresInRegion(int regionId)
        {
            var region = await _context.Regions.FirstOrDefaultAsync(r => r.Id == regionId);
            if (region is null)
            {
                throw new ArgumentException("Region not found.");
            }
            var places = await _context.Places.Include(p => p.Region).Where(p => p.RegionId == regionId).ToListAsync();
            if (places is null)
            {
                return new List<StoreModel>();
            }
            var tasks = places.Select(p => GetAllStoresInPlace(p.Id));

            // Wait for all of them
            var results = await Task.WhenAll(tasks);

            // Flatten the list of lists
            var allItems = results.SelectMany(r => r).ToList();
            return allItems;
        }

        public async Task<IEnumerable<StoreModel>> GetAllStoresInPlace(int placeId)
        {
            // thread safe
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
                var stores = await dbContext.Stores
                    .Include(s => s.category)
                    .Include(s => s.place)
                    .Include(s => s.place.Region)
                    .Where(s => s.placeId == placeId)
                    .ToListAsync();
                return stores;
            }
        }
    }
}