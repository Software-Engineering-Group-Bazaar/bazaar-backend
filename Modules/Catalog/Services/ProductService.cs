using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Catalog.Models;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Services
{
    public class ProductService : IProductService
    {
        private readonly CatalogDbContext _context;

        public ProductService(CatalogDbContext context, ILogger<ProductService> logger) // Logger je opcionalan
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<IEnumerable<Product>> GetAllProductsAsync()
        {
            return await _context.Products
                                 .Include(p => p.ProductCategory) // Uključi povezanu kategoriju
                                 .AsNoTracking()
                                 .ToListAsync();
        }

        public async Task<Product?> GetProductByIdAsync(int id)
        {
            return await _context.Products
                                 .Include(p => p.ProductCategory) // Uključi povezanu kategoriju
                                 .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<IEnumerable<Product>> GetProductsByCategoryIdAsync(int categoryId)
        {
            if (categoryId <= 0) return new List<Product>(); // Vrati praznu listu za nevažeći ID
            // Filtriramo po ID-u unutar navigacijskog svojstva
            return await _context.Products
                                 .Include(p => p.ProductCategory)
                                 .Where(p => p.ProductCategory.Id == categoryId) // Filter po ID-u kategorije
                                 .AsNoTracking()
                                 .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetProductsByStoreIdAsync(int storeId)
        {
            if (storeId <= 0) return new List<Product>(); // Vrati praznu listu za nevažeći ID

            return await _context.Products
                                 .Include(p => p.ProductCategory)
                                 .Where(p => p.StoreId == storeId) // Direktan filter po StoreId
                                 .AsNoTracking()
                                 .ToListAsync();
        }

        public async Task<Product> CreateProductAsync(Product product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            if (product.ProductCategory == null || product.ProductCategory.Id <= 0)
                throw new ArgumentException("ProductCategory s validnim ID-jem je obavezan.", nameof(product.ProductCategory));
            if (string.IsNullOrWhiteSpace(product.Name))
                throw new ArgumentException("Naziv proizvoda je obavezan.", nameof(product.Name));
            if (product.RetailPrice < 0 || product.WholesalePrice < 0)
                throw new ArgumentException("Cijene moraju biti pozitivne.", nameof(product.RetailPrice));
            if (product.StoreId <= 0)
                throw new ArgumentException("StoreId je obavezan.", nameof(product.StoreId));


            var existingCategory = await _context.ProductCategories.FindAsync(product.ProductCategory.Id);
            if (existingCategory == null)
            {
                throw new InvalidOperationException($"Kategorija proizvoda s ID-om {product.ProductCategory.Id} ne postoji.");
            }

            product.Id = 0;

            product.ProductCategory = existingCategory;

            _context.Products.Add(product);

            try
            {
                await _context.SaveChangesAsync();
                return product;
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Došlo je do greške prilikom spremanja proizvoda.", ex);
            }
        }

        public async Task<bool> UpdateProductAsync(Product product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            if (product.Id <= 0)
                throw new ArgumentException("Validan ID proizvoda je potreban za ažuriranje.", nameof(product.Id));
            if (product.ProductCategory == null || product.ProductCategory.Id <= 0)
                throw new ArgumentException("ProductCategory s validnim ID-jem je obavezan.", nameof(product.ProductCategory));

            var existingProduct = await _context.Products
                                            .Include(p => p.ProductCategory)
                                            .FirstOrDefaultAsync(p => p.Id == product.Id);

            if (existingProduct == null)
            {
                return false;
            }

            if (existingProduct.ProductCategory.Id != product.ProductCategory.Id)
            {
                var newCategory = await _context.ProductCategories.FindAsync(product.ProductCategory.Id);
                if (newCategory == null)
                {
                    throw new InvalidOperationException($"Kategorija proizvoda s ID-om {product.ProductCategory.Id} ne postoji.");
                }
                existingProduct.ProductCategory = newCategory;
            }

            existingProduct.Name = product.Name;
            existingProduct.RetailPrice = product.RetailPrice;
            existingProduct.WholesalePrice = product.WholesalePrice;
            existingProduct.Weight = product.Weight;
            existingProduct.WeightUnit = product.WeightUnit;
            existingProduct.Volume = product.Volume;
            existingProduct.VolumeUnit = product.VolumeUnit;
            existingProduct.StoreId = product.StoreId;

            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                var stillExists = await _context.Products.AnyAsync(p => p.Id == product.Id);
                if (!stillExists)
                {
                    return false;
                }
                else
                {
                    throw;
                }
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Došlo je do greške prilikom ažuriranja proizvoda.", ex);
            }
        }

        public async Task<bool> DeleteProductAsync(int id)
        {
            if (id <= 0)
                return false;

            var productToDelete = await _context.Products.FindAsync(id);

            if (productToDelete == null)
            {
                return false;
            }

            _context.Products.Remove(productToDelete);

            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Nije moguće obrisati proizvod. Možda postoje povezane stavke.", ex);
            }
        }
    }
}