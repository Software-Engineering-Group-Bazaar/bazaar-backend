using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdminApi.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Store.Interface;
using Store.Models;

namespace Store.Services
{
    public class StoreCategoryService : IStoreCategoryService
    {
        private readonly StoreDbContext _context;
        private readonly ILogger<StoreCategoryService> _logger;

        public StoreCategoryService(StoreDbContext context, ILogger<StoreCategoryService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<StoreCategoryDto> CreateCategoryAsync(StoreCategoryCreateDto createDto)
        {
            if (createDto == null) throw new ArgumentNullException(nameof(createDto));
            if (string.IsNullOrWhiteSpace(createDto.Name)) throw new ArgumentException("Category name cannot be empty.", nameof(createDto.Name));

            bool nameExists = await _context.StoreCategories
                                          .AnyAsync(c => c.Name.ToLower() == createDto.Name.ToLower());
            if (nameExists)
            {
                throw new InvalidOperationException($"Store category with name '{createDto.Name}' already exists.");
            }

            var category = new StoreCategory
            {
                Name = createDto.Name // Koristi PascalCase
            };

            _context.StoreCategories.Add(category);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created new store category with ID {Id} and Name {Name}", category.Id, category.Name);

            return new StoreCategoryDto { Id = category.Id, Name = category.Name }; // Vraća puni DTO
        }

        public async Task<IEnumerable<StoreCategoryDto>> GetAllCategoriesAsync()
        {
            var categories = await _context.StoreCategories
                                           .AsNoTracking()
                                           .OrderBy(c => c.Name)
                                           .ToListAsync();
            return categories.Select(c => new StoreCategoryDto { Id = c.Id, Name = c.Name }).ToList();
        }

        public async Task<StoreCategoryDto?> GetCategoryByIdAsync(int id)
        {
            if (id <= 0) return null;
            var category = await _context.StoreCategories.FindAsync(id);
            if (category == null) return null;
            return new StoreCategoryDto { Id = category.Id, Name = category.Name };
        }

        public async Task<StoreCategoryDto?> UpdateCategoryAsync(int id, StoreCategoryDto updateDto)
        {
            if (id <= 0) throw new ArgumentException("Category ID must be positive.", nameof(id));
            if (updateDto == null) throw new ArgumentNullException(nameof(updateDto));
            if (string.IsNullOrWhiteSpace(updateDto.Name)) throw new ArgumentException("Category name cannot be empty.", nameof(updateDto.Name));
            // Opciono: Provjeri if (id != updateDto.Id) ako DTO ima ID

            var existingCategory = await _context.StoreCategories.FindAsync(id);
            if (existingCategory == null) return null; // Not Found

            bool nameExistsOnOther = await _context.StoreCategories
                                         .AnyAsync(c => c.Id != id && c.Name.ToLower() == updateDto.Name.ToLower());
            if (nameExistsOnOther)
            {
                throw new InvalidOperationException($"Another store category with name '{updateDto.Name}' already exists.");
            }

            existingCategory.Name = updateDto.Name; // Koristi PascalCase

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated store category with ID {Id}", id);
                return new StoreCategoryDto { Id = existingCategory.Id, Name = existingCategory.Name };
            }
            catch (DbUpdateConcurrencyException ex) { /* ... kao prije ... */ throw; }
            catch (DbUpdateException ex) { /* ... kao prije ... */ throw; }
        }

        public async Task<bool> DeleteCategoryAsync(int id)
        {
            if (id <= 0) throw new ArgumentException("Category ID must be positive.", nameof(id));
            var categoryToRemove = await _context.StoreCategories.FindAsync(id);
            if (categoryToRemove == null) return false;
            bool isInUse = await _context.Stores.AnyAsync(s => s.StoreCategoryId == id);
            if (isInUse) throw new InvalidOperationException("Cannot delete category because it is assigned to one or more stores.");
            _context.StoreCategories.Remove(categoryToRemove);
            try
            {
                return await _context.SaveChangesAsync() > 0;
            }
            catch (DbUpdateException ex) { /* ... kao prije ... */ throw; }
        }

        // Ova ostaje interna za sada
        public async Task<IEnumerable<StoreModel>> GetStoresInCategoryAsync(int categoryId)
        {
            if (categoryId <= 0) return new List<StoreModel>();
            return await _context.Stores
                                 .Where(s => s.StoreCategoryId == categoryId)
                                 .Include(s => s.StoreCategory)
                                 .AsNoTracking()
                                 .ToListAsync();
        }
    }
}