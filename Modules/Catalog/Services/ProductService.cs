using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Catalog.DTO;
using Catalog.Dtos;
using Catalog.Interfaces;
using Catalog.Models;
using Inventory.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Store.Models;
using Users.Interface;
using Users.Models;

namespace Catalog.Services
{
    public class ProductService : IProductService
    {
        private readonly CatalogDbContext _context;
        private readonly StoreDbContext _storeContext;
        private readonly IImageStorageService _imageStorageService;
        private readonly UserManager<User> _userManager;

        private readonly InventoryDbContext _inventoryContext;

        public ProductService(CatalogDbContext context,
                              StoreDbContext storeContext,
                              IImageStorageService imageStorageService,
                              UserManager<User> userManager,
                              InventoryDbContext inventoryContext,
                              ILogger<ProductService> logger) // Logger je opcionalan
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _storeContext = storeContext ?? throw new ArgumentNullException(nameof(storeContext));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _imageStorageService = imageStorageService;
            _inventoryContext = inventoryContext;
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
            product.CreatedAt = new DateTime();
            await _context.Products.AddAsync(product);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Došlo je do greške prilikom spremanja proizvoda.", ex);
            }

            var newInventory = new Inventory.Models.Inventory
            {
                ProductId = product.Id,
                StoreId = product.StoreId,
                Quantity = 0,
                OutOfStock = true,
                LastUpdated = DateTime.UtcNow
            };

            try
            {
                bool alreadyExists = await _inventoryContext.Inventories.AnyAsync(inv => inv.ProductId == newInventory.ProductId && inv.StoreId == newInventory.StoreId);
                if (!alreadyExists)
                {
                    _inventoryContext.Inventories.Add(newInventory);
                    await _inventoryContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GREŠKA prilikom kreiranja inventara za Product ID {product.Id}: {ex.Message}"); // Minimalni ispis greške
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
            // TODO: los stil je baciti izuzetak ako se proizvod ne nadje, to nije IZUZETNA situacija vratite null
            if (productId <= 0) throw new ArgumentException("Product ID must be positive.", nameof(productId));
            if (pricingData == null) throw new ArgumentNullException(nameof(pricingData));
            if (string.IsNullOrWhiteSpace(sellerUserId)) throw new ArgumentNullException(nameof(sellerUserId));

            // 1. Dohvati Proizvod
            var product = await _context.Products
                // Uključi navigaciona svojstva potrebna za DTO i validaciju
                .Include(p => p.ProductCategory)
                .Include(p => p.Pictures)
                .FirstOrDefaultAsync(p => p.Id == productId);
            if (product is null)
            {
                throw new KeyNotFoundException("Requesting user not found.");
            }
            // 2. *** ISPRAVNA PROVJERA VLASNIŠTVA ***
            // Pronađi korisnika (Sellera) koji je poslao zahtjev
            var requestingSeller = await _userManager.FindByIdAsync(sellerUserId);
            if (requestingSeller == null)
            {
                throw new KeyNotFoundException("Requesting user not found."); // Ili vrati null/Forbid?
            }

            // Provjeri da li StoreId korisnika odgovara StoreId proizvoda
            // (Pretpostavka: User model ima nullable int StoreId)
            if (requestingSeller.StoreId != product.StoreId)
            {
                // Vrati null ili baci UnauthorizedAccessException da kontroler vrati 403 Forbidden
                // return null;
                throw new UnauthorizedAccessException("User is not authorized to update pricing for this product.");
            }
            // --- KRAJ PROVJERE VLASNIŠTVA ---

            // 3. Validacija Cijena
            if (pricingData.RetailPrice.HasValue && pricingData.RetailPrice <= 0)
                throw new ArgumentException("Retail price must be greater than 0 if provided.");

            // Pretpostavljamo da WholesaleThreshold može biti 0 ili veći
            if (pricingData.WholesaleThreshold.HasValue && pricingData.WholesaleThreshold < 0)
                throw new ArgumentException("Wholesale threshold cannot be negative if provided.");

            if (pricingData.WholesalePrice.HasValue && pricingData.WholesalePrice < 0)
                throw new ArgumentException("Wholesale price cannot be negative if provided.");

            // Odredi finalnu maloprodajnu cijenu za poređenje
            decimal finalRetailPrice = pricingData.RetailPrice ?? product.RetailPrice;
            if (pricingData.WholesalePrice.HasValue && pricingData.WholesalePrice > finalRetailPrice)
                throw new ArgumentException("Wholesale price cannot exceed retail price.");

            // Logika za WholesalePrice ako nema praga
            if (!pricingData.WholesaleThreshold.HasValue && pricingData.WholesalePrice.HasValue)
            {
                pricingData.WholesalePrice = null; // Postavi na null ako nema praga
            }

            // 4. Ažuriraj Polja Proizvoda (
            product.RetailPrice = pricingData.RetailPrice ?? product.RetailPrice;

            product.WholesaleThreshold = pricingData.WholesaleThreshold;

            product.WholesalePrice = pricingData.WholesalePrice;

            // 5. Sačuvaj Promjene
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Products.AnyAsync(p => p.Id == productId)) return null;
                else throw;
            }
            catch (DbUpdateException ex)
            {
                throw new Exception("Database error occurred during product pricing update.", ex);
            }

            // 6. Mapiraj ažurirani entitet u DTO za povratak
            return new ProductGetDto
            {
                Id = product.Id,
                Name = product.Name,
                RetailPrice = product.RetailPrice,
                WholesaleThreshold = product.WholesaleThreshold, // DTO polje treba biti int?
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

        public async Task<bool> UpdateProductAvailabilityAsync(string sellerUserId, int productId, bool isActive)
        {
            if (productId <= 0) throw new ArgumentException("Product ID must be positive.", nameof(productId));
            if (string.IsNullOrWhiteSpace(sellerUserId)) throw new ArgumentNullException(nameof(sellerUserId));

            // 1. Dohvati Proizvod
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);
            if (product == null)
            {
                return false;
            }

            // 2. Ažuriraj Polje
            if (product.IsActive == isActive) // Koristimo IsActive iz modela
            {
                return true;
            }

            product.IsActive = isActive; // Postavi novu vrijednost
            _context.Entry(product).State = EntityState.Modified;

            // 3. Sačuvaj
            try
            {
                await _context.SaveChangesAsync();
                return true; // Uspjeh
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Products.AnyAsync(p => p.Id == productId)) return false; // Više ne postoji
                else throw;
            }
            catch (DbUpdateException ex)
            {
                throw new Exception("Database error occurred during product availability update.", ex);
            }
        }

        public async Task<bool> UpdateProductPointRateAsync(int productId, double pointRate)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                throw new ArgumentException($"Can't find product with id: {productId}");
            }

            if (pointRate < 0)
            {
                throw new ArgumentException($"Point rate must be greater or equal 0");
            }

            product.PointRate = pointRate;

            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Products.AnyAsync(p => p.Id == productId)) return false;
                else throw;
            }
            catch (DbUpdateException ex)
            {
                throw new Exception("Database error occurred during product point rate update.", ex);
            }
        }
    }
}
