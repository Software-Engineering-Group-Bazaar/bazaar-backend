using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Catalog.Models;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Services
{
    public class ProductCategoryService : IProductCategoryService
    {
        private readonly CatalogDbContext _context;

        public ProductCategoryService(CatalogDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<IEnumerable<ProductCategory>> GetAllCategoriesAsync()
        {
            return await _context.ProductCategories
                                 .AsNoTracking()
                                 .ToListAsync();
        }

        public async Task<ProductCategory?> GetCategoryByIdAsync(int id)
        {
            if (id <= 0) return null;

            return await _context.ProductCategories.FindAsync(id);
        }
        public async Task<ProductCategory?> GetCategoryByNameAsync(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return await _context.ProductCategories.FirstOrDefaultAsync(c => c.Name == name);
        }


        public async Task<ProductCategory> CreateCategoryAsync(ProductCategory category)
        {
            if (category == null)
                throw new ArgumentNullException(nameof(category));
            if (string.IsNullOrWhiteSpace(category.Name))
                throw new ArgumentException("Naziv kategorije ne smije biti prazan.", nameof(category.Name));

            category.Id = 0;

            bool nameExists = await _context.ProductCategories
                                          .AnyAsync(c => c.Name.ToLower() == category.Name.ToLower());
            if (nameExists)
            {
                throw new InvalidOperationException($"Kategorija s nazivom '{category.Name}' već postoji.");
            }

            _context.ProductCategories.Add(category);

            await _context.SaveChangesAsync();

            return category;
        }

        public async Task<bool> UpdateCategoryAsync(ProductCategory category)
        {
            if (category == null)
                throw new ArgumentNullException(nameof(category));
            if (string.IsNullOrWhiteSpace(category.Name))
                throw new ArgumentException("Naziv kategorije ne smije biti prazan.", nameof(category.Name));
            if (category.Id <= 0)
                throw new ArgumentException("ID kategorije za ažuriranje mora biti pozitivan broj.", nameof(category.Id));

            bool nameExistsOnOther = await _context.ProductCategories
                                         .AnyAsync(c => c.Id != category.Id && c.Name.ToLower() == category.Name.ToLower());
            if (nameExistsOnOther)
            {
                throw new InvalidOperationException($"Druga kategorija s nazivom '{category.Name}' već postoji.");
            }

            var existingCategory = await _context.ProductCategories.FindAsync(category.Id);

            if (existingCategory == null)
            {
                return false;
            }

            existingCategory.Name = category.Name;

            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {

                if (!await _context.ProductCategories.AnyAsync(c => c.Id == category.Id))
                {
                    return false;
                }
                else
                {
                    throw;
                }
            }
        }

        public async Task<bool> DeleteCategoryAsync(int id)
        {
            if (id <= 0)
            {
                throw new ArgumentException("ID kategorije za brisanje mora biti pozitivan broj.", nameof(id));
            }

            var categoryToRemove = await _context.ProductCategories.FindAsync(id);

            if (categoryToRemove == null)
            {
                return false;
            }

            _context.ProductCategories.Remove(categoryToRemove);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}