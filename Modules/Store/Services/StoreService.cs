using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdminApi.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Store.Interface;
using Store.Models;
using Users.Models; // Importuj tvoj User model

namespace Store.Services
{
    public class StoreService : IStoreService
    {
        private readonly StoreDbContext _storeContext;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<StoreService> _logger;
        // private readonly IMapper _mapper;

        public StoreService(StoreDbContext storeContext, UserManager<User> userManager, ILogger<StoreService> logger /*, IMapper mapper*/)
        {
            _storeContext = storeContext ?? throw new ArgumentNullException(nameof(storeContext));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            // _mapper = mapper;
        }

        public async Task<StoreGetDto?> CreateStoreForSellerAsync(string sellerUserId, StoreCreateDto createDto)
        {
            if (string.IsNullOrWhiteSpace(sellerUserId)) throw new ArgumentNullException(nameof(sellerUserId));
            if (createDto == null) throw new ArgumentNullException(nameof(createDto));

            var sellerUser = await _userManager.FindByIdAsync(sellerUserId);
            if (sellerUser == null) throw new KeyNotFoundException($"User with ID {sellerUserId} not found.");
            if (sellerUser.StoreId.HasValue) throw new InvalidOperationException("User already has a store associated.");

            var categoryEntity = await _storeContext.StoreCategories.FindAsync(createDto.CategoryId);
            if (categoryEntity == null) throw new ArgumentException($"Store Category with ID {createDto.CategoryId} not found.");

            var newStore = new StoreModel
            {
                Name = createDto.Name,
                StoreCategoryId = categoryEntity.Id,
                // Koristi nova polja za adresu i PascalCase
                StreetAndNumber = createDto.StreetAndNumber,
                City = createDto.City,
                Municipality = createDto.Municipality,
                PostalCode = createDto.PostalCode,
                Country = createDto.Country,
                Description = createDto.Description,
                IsActive = true,
            };

            _storeContext.Stores.Add(newStore);

            using var transaction = await _storeContext.Database.BeginTransactionAsync();
            try
            {
                await _storeContext.SaveChangesAsync(); // Sačuvaj Store
                _logger.LogInformation("Created new store with ID {StoreId} for user {UserId}.", newStore.Id, sellerUserId);

                sellerUser.StoreId = newStore.Id; // Poveži Usera
                var updateResult = await _userManager.UpdateAsync(sellerUser);

                if (!updateResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError("Failed to update user {UserId} with StoreId {StoreId}. Rolled back. Errors: {Errors}", sellerUserId, newStore.Id, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                    throw new Exception($"Failed to associate store with user. Errors: {string.Join(", ", updateResult.Errors.Select(e => e.Description))}");
                }

                await transaction.CommitAsync();
                _logger.LogInformation("Successfully associated store {StoreId} with user {UserId}", newStore.Id, sellerUserId);

                // Mapiraj u DTO
                return new StoreGetDto
                {
                    Id = newStore.Id,
                    Name = newStore.Name,
                    CategoryName = categoryEntity.Name,
                    IsActive = newStore.IsActive,
                    StreetAndNumber = newStore.StreetAndNumber, // Nova polja
                    City = newStore.City,
                    Municipality = newStore.Municipality,
                    PostalCode = newStore.PostalCode,
                    Country = newStore.Country,
                    Description = newStore.Description
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error during store creation transaction for user {UserId}", sellerUserId);
                throw new Exception("An error occurred during the store creation process.", ex);
            }
        }

        // Implementiraj CreateStoreForAdminAsync ako treba (slično, ali prima TargetSellerUserId iz DTO)

        public async Task<IEnumerable<StoreGetDto>> GetAllStoresAsync()
        {
            var stores = await _storeContext.Stores
                                       .Include(s => s.StoreCategory)
                                       .AsNoTracking()
                                       .OrderBy(s => s.Name)
                                       .ToListAsync();

            // Mapiraj u DTO
            return stores.Select(s => new StoreGetDto
            {
                Id = s.Id,
                Name = s.Name,
                CategoryName = s.StoreCategory?.Name ?? "Unknown",
                IsActive = s.IsActive,
                StreetAndNumber = s.StreetAndNumber, // Nova polja
                City = s.City,
                Municipality = s.Municipality,
                PostalCode = s.PostalCode,
                Country = s.Country,
                Description = s.Description
            }).ToList();
        }

        public async Task<StoreGetDto?> GetStoreByIdAsync(int id)
        {
            if (id <= 0) return null;
            var store = await _storeContext.Stores
                                       .Include(s => s.StoreCategory)
                                       .AsNoTracking()
                                       .FirstOrDefaultAsync(s => s.Id == id);
            if (store == null) return null;

            // Mapiraj u DTO
            return new StoreGetDto
            {
                Id = store.Id,
                Name = store.Name,
                CategoryName = store.StoreCategory?.Name ?? "Unknown",
                IsActive = store.IsActive,
                StreetAndNumber = store.StreetAndNumber, // Nova polja
                City = store.City,
                Municipality = store.Municipality,
                PostalCode = store.PostalCode,
                Country = store.Country,
                Description = store.Description
            };
        }

        public async Task<StoreGetDto?> UpdateStoreAsync(int id, StoreUpdateDto updateDto, string requestingUserId, bool isAdmin)
        {
            if (id <= 0) throw new ArgumentException("Store ID must be positive.", nameof(id));
            if (updateDto == null) throw new ArgumentNullException(nameof(updateDto));
            if (string.IsNullOrWhiteSpace(requestingUserId)) throw new ArgumentNullException(nameof(requestingUserId));

            var store = await _storeContext.Stores
                                      .Include(s => s.StoreCategory) // Učitaj radi imena kategorije za povratni DTO
                                      .FirstOrDefaultAsync(s => s.Id == id);
            if (store == null) return null; // NotFound

            // --- AUTORIZACIJA ---
            if (!isAdmin)
            {
                var ownerUser = await _userManager.Users.AsNoTracking().FirstOrDefaultAsync(u => u.StoreId == id);
                if (ownerUser == null || ownerUser.Id != requestingUserId)
                {
                    throw new UnauthorizedAccessException("User is not authorized to update this store.");
                }
            }

            // Provjeri novu kategoriju
            StoreCategory? categoryEntity = store.StoreCategory; // Trenutna kategorija
            if (store.StoreCategoryId != updateDto.CategoryId)
            {
                categoryEntity = await _storeContext.StoreCategories.FindAsync(updateDto.CategoryId);
                if (categoryEntity == null) throw new ArgumentException($"Store Category with ID {updateDto.CategoryId} not found.");
                store.StoreCategoryId = categoryEntity.Id;
            }

            // Ažuriraj ostala polja
            store.Name = updateDto.Name;
            store.StreetAndNumber = updateDto.StreetAndNumber; // Nova polja
            store.City = updateDto.City;
            store.Municipality = updateDto.Municipality;
            store.PostalCode = updateDto.PostalCode;
            store.Country = updateDto.Country;
            store.Description = updateDto.Description;
            store.IsActive = updateDto.IsActive;

            try
            {
                await _storeContext.SaveChangesAsync();
                _logger.LogInformation("Store {StoreId} updated by User {UserId} (IsAdmin: {IsAdmin})", id, requestingUserId, isAdmin);

                // Mapiraj u DTO
                return new StoreGetDto
                {
                    Id = store.Id,
                    Name = store.Name,
                    CategoryName = categoryEntity?.Name ?? "Unknown", // Koristi dohvaćenu ili staru
                    IsActive = store.IsActive,
                    StreetAndNumber = store.StreetAndNumber, // Nova polja
                    City = store.City,
                    Municipality = store.Municipality,
                    PostalCode = store.PostalCode,
                    Country = store.Country,
                    Description = store.Description
                };
            }
            catch (DbUpdateConcurrencyException ex) { /* ... */ throw; }
            catch (DbUpdateException ex) { /* ... */ throw; }
        }

        public async Task<bool> DeleteStoreAsync(int id, string requestingUserId, bool isAdmin)
        {
            // Implementacija ostaje ista kao u prethodnom odgovoru (sa provjerom, uklanjanjem StoreId sa Usera, transakcijom)
            if (id <= 0) throw new ArgumentException("Store ID must be positive.", nameof(id));
            if (string.IsNullOrWhiteSpace(requestingUserId)) throw new ArgumentNullException(nameof(requestingUserId));

            var store = await _storeContext.Stores.FindAsync(id);
            if (store == null) return false; // NotFound

            var ownerUser = await _userManager.Users.FirstOrDefaultAsync(u => u.StoreId == id);
            string? ownerUserId = ownerUser?.Id;

            if (!isAdmin)
            {
                if (ownerUserId == null || ownerUserId != requestingUserId)
                {
                    throw new UnauthorizedAccessException("User is not authorized to delete this store.");
                }
            }

            using var transaction = await _storeContext.Database.BeginTransactionAsync();
            try
            {
                if (ownerUser != null)
                {
                    ownerUser.StoreId = null;
                    var updateResult = await _userManager.UpdateAsync(ownerUser);
                    if (!updateResult.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError("Failed to remove StoreId from user {UserId} during store deletion. Errors: {Errors}", ownerUserId, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                        throw new Exception("Failed to disassociate user from store before deletion.");
                    }
                    _logger.LogInformation("Disassociated StoreId {StoreId} from User {UserId} before deletion.", id, ownerUserId);
                }
                else
                {
                    _logger.LogWarning("Could not find owner user for Store {StoreId} during deletion process.", id);
                }

                // Opciono: Obriši proizvode? Ili spriječi ako postoje proizvodi?
                // var productsExist = await _catalogContext.Products.AnyAsync(p => p.StoreId == id); // Treba CatalogDbContext
                // if (productsExist) throw new InvalidOperationException("Cannot delete store with existing products.");

                _storeContext.Stores.Remove(store);
                var storeDeleteResult = await _storeContext.SaveChangesAsync();

                await transaction.CommitAsync();
                _logger.LogInformation("Store {StoreId} deleted by User {UserId} (IsAdmin: {IsAdmin})", id, requestingUserId, isAdmin);
                return storeDeleteResult > 0;
            }
            catch (Exception ex) // Hvata greške iz SaveChangesAsync, UserManager.UpdateAsync, Commit/Rollback
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error during store deletion transaction for Store ID {StoreId}", id);
                // Prepakuj originalnu grešku ako je DbUpdateException i sadrži info o FK
                if (ex is DbUpdateException dbEx)
                {
                    throw new InvalidOperationException("Could not delete the store due to database constraints (possibly related data exists).", dbEx);
                }
                throw new Exception("An error occurred during the store deletion process.", ex);
            }
        }
    }
}