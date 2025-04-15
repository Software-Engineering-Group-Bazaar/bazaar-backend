using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Catalog.DTO;
using Catalog.Dtos;
using Catalog.Interfaces;
using Catalog.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Store.Models;

namespace Catalog.Services
{
    public class ProductService : IProductService
    {
        private readonly CatalogDbContext _context;
        private readonly StoreDbContext _storeContext;
        private readonly IImageStorageService _imageStorageService;

        public ProductService(CatalogDbContext context,
                              StoreDbContext storeContext,
                              IImageStorageService imageStorageService,
                              ILogger<ProductService> logger) // Logger je opcionalan
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _storeContext = storeContext ?? throw new ArgumentNullException(nameof(storeContext));
            _imageStorageService = imageStorageService;
        }

        public async Task<IEnumerable<Product>> GetAllProductsAsync()
        {
            return await _context.Products
                                 .Include(p => p.ProductCategory)
                                 .Include(p => p.Pictures)
                                 .AsNoTracking()
                                 .ToListAsync();
        }

        public async Task<Product?> GetProductByIdAsync(int id)
        {
            return await _context.Products
                                 .Include(p => p.ProductCategory)
                                 .Include(p => p.Pictures)
                                 .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<IEnumerable<Product>> GetProductsByCategoryIdAsync(int categoryId)
        {
            if (categoryId <= 0) return new List<Product>();
            return await _context.Products
                                 .Include(p => p.Pictures)
                                 .Include(p => p.ProductCategory)
                                 .Where(p => p.ProductCategory.Id == categoryId)
                                 .AsNoTracking()
                                 .ToListAsync();
        }

        public async Task<IEnumerable<Product>> GetProductsByStoreIdAsync(int storeId)
        {
            if (storeId <= 0) return new List<Product>();

            return await _context.Products
                                 .Include(p => p.ProductCategory)
                                 .Include(p => p.Pictures)
                                 .Where(p => p.StoreId == storeId)
                                 .AsNoTracking()
                                 .ToListAsync();
        }

        public async Task<Product> CreateProductAsync(Product product, List<IFormFile>? files)
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
                throw new InvalidOperationException($"Kategorija proizvoda s ID-om {product.ProductCategory.Id} ne postoji.");

            product.Id = 0;
            product.ProductCategory = existingCategory;

            await _context.Products.AddAsync(product);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Došlo je do greške prilikom spremanja proizvoda.", ex);
            }

            if (files is not null)
            {
                foreach (var file in files)
                {
                    var path = await _imageStorageService.UploadImageAsync(file, null);
                    if (path is null)
                        throw new InvalidDataException("Path not created for image!");

                    var pic = new ProductPicture
                    {
                        Url = path,
                        ProductId = product.Id,
                    };
                    await _context.AddAsync(pic);
                }
            }

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
                                                .Include(p => p.Pictures)
                                                .FirstOrDefaultAsync(p => p.Id == product.Id);

            if (existingProduct == null)
                return false;

            if (existingProduct.ProductCategory.Id != product.ProductCategory.Id)
            {
                var newCategory = await _context.ProductCategories.FindAsync(product.ProductCategory.Id);
                if (newCategory == null)
                    throw new InvalidOperationException($"Kategorija proizvoda s ID-om {product.ProductCategory.Id} ne postoji.");

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
                    return false;
                else
                    throw;
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
                return false;

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

        public async Task<bool> DeleteProductFromStoreAsync(int storeId)
        {
            var productsToDelete = await _context.Products.Where(p => p.StoreId == storeId).ToListAsync();
            _context.Products.RemoveRange(productsToDelete);
            await _context.SaveChangesAsync();
            return productsToDelete.Count > 0;
        }

        public async Task<IEnumerable<Product>> SearchProductsByNameAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return await _context.Products.Include(p => p.ProductCategory).Include(p => p.Pictures).ToListAsync();
            }

            var normalizedSearchTerm = searchTerm.Trim().ToLower();

            var products = await _context.Products
                .Include(p => p.ProductCategory)
                .Include(p => p.Pictures)
                .Where(p => p.Name.ToLower().Contains(normalizedSearchTerm))
                .ToListAsync();

            return products;
        }

        public async Task<ProductGetDto?> UpdateProductPricingAsync(string sellerUserId, int productId, UpdateProductPricingRequestDto pricingData)
        {
            var product = await _context.Products
                .Include(p => p.ProductCategory)
                .Include(p => p.Pictures)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
                return null;

            var store = await _storeContext.Stores.FirstOrDefaultAsync(s => s.id == product.StoreId);
            if (store == null || store.SellerUserId != sellerUserId)
                return null;

            if (pricingData.RetailPrice <= 0)
                throw new ArgumentException("Retail price must be greater than 0.");

            if (pricingData.WholesaleThreshold < 0)
                throw new ArgumentException("Wholesale threshold cannot be negative.");

            if (pricingData.WholesalePrice < 0)
                throw new ArgumentException("Wholesale price cannot be negative.");

            if (pricingData.WholesalePrice > pricingData.RetailPrice)
                throw new ArgumentException("Wholesale price cannot exceed retail price.");

            product.RetailPrice = pricingData.RetailPrice ?? product.RetailPrice;
            product.WholesalePrice = pricingData.WholesalePrice ?? product.WholesalePrice;
            // product.WholesaleThreshold = pricingData.WholesaleThreshold ?? product.WholesaleThreshold;

            await _context.SaveChangesAsync();

            return new ProductGetDto
            {
                Id = product.Id,
                Name = product.Name,
                RetailPrice = product.RetailPrice,
                WholesalePrice = product.WholesalePrice,
                Weight = product.Weight,
                WeightUnit = product.WeightUnit,
                Volume = product.Volume,
                VolumeUnit = product.VolumeUnit,
                StoreId = product.StoreId,
                ProductCategory = new ProductCategoryGetDto
                {
                    Id = product.ProductCategoryId,
                    Name = product.ProductCategory?.Name ?? "undefined"
                },
                Photos = product.Pictures?.Select(p => p.Url).ToList() ?? new List<string>()
            };
        }
    }
}
