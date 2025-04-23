using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Catalog.Models;
using Inventory.Dtos;
using Inventory.Interfaces;
using Inventory.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Store.Models;
using Users.Models;

namespace Inventory.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly InventoryDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<InventoryService> _logger;
        private readonly CatalogDbContext _catalogContext;
        private readonly StoreDbContext _storeContext;

        public InventoryService(
            InventoryDbContext context,
            UserManager<User> userManager,
            ILogger<InventoryService> logger,
            CatalogDbContext catalogContext,
            StoreDbContext storeContext
            )
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _catalogContext = catalogContext ?? throw new ArgumentNullException(nameof(catalogContext));
            _storeContext = storeContext ?? throw new ArgumentNullException(nameof(storeContext));
            // _dbNotificationService = dbNotificationService;
            // _pushNotificationService = pushNotificationService;
        }

        public async Task<InventoryDto?> CreateInventoryAsync(string requestingUserId, bool isAdmin, CreateInventoryRequestDto createDto)
        {
            _logger.LogInformation("Attempting to create inventory record by User {UserId} (IsAdmin: {IsAdmin}). DTO: {@CreateDto}",
                                   requestingUserId, isAdmin, createDto);

            if (createDto == null) throw new ArgumentNullException(nameof(createDto));

            int targetStoreId = createDto.StoreId;
            if (!isAdmin)
            {
                var sellerUser = await _userManager.FindByIdAsync(requestingUserId);
                if (sellerUser == null) throw new KeyNotFoundException("Requesting user not found.");

                if (!sellerUser.StoreId.HasValue)
                {
                    _logger.LogWarning("Seller {UserId} attempted to create inventory but has no StoreId assigned.", requestingUserId);
                    throw new UnauthorizedAccessException("Seller does not have an associated store.");
                }
                targetStoreId = sellerUser.StoreId.Value;
                _logger.LogInformation("Seller {UserId} is creating inventory for their StoreId {StoreId}.", requestingUserId, targetStoreId);
            }
            else
            {
                _logger.LogInformation("Admin {UserId} is creating inventory for StoreId {StoreId} from DTO.", requestingUserId, targetStoreId);
            }

            // --- Validacija Postojanja Proizvoda i Prodavnice ---
            bool productExists = await _catalogContext.Products.AnyAsync(p => p.Id == createDto.ProductId);
            if (!productExists)
            {
                _logger.LogWarning("Inventory creation failed: Product with ID {ProductId} not found.", createDto.ProductId);
                throw new KeyNotFoundException($"Product with ID {createDto.ProductId} not found.");
            }

            bool storeExists = await _storeContext.Stores.AnyAsync(s => s.id == targetStoreId);
            if (!storeExists)
            {
                _logger.LogWarning("Inventory creation failed: Store with ID {StoreId} not found.", targetStoreId);
                throw new KeyNotFoundException($"Store with ID {targetStoreId} not found.");
            }

            bool alreadyExists = await _context.Inventories.AnyAsync(inv => inv.ProductId == createDto.ProductId && inv.StoreId == targetStoreId);
            if (alreadyExists)
            {
                _logger.LogWarning("Inventory creation failed: Record already exists for ProductId {ProductId} and StoreId {StoreId}.", createDto.ProductId, targetStoreId);
                throw new InvalidOperationException($"Inventory record already exists for Product ID {createDto.ProductId} in Store ID {targetStoreId}.");
            }

            // --- Kreiranje Entiteta ---
            var newInventory = new Models.Inventory
            {
                ProductId = createDto.ProductId,
                StoreId = targetStoreId,
                Quantity = createDto.InitialQuantity,
                OutOfStock = createDto.InitialQuantity <= 0,
                LastUpdated = DateTime.UtcNow
            };

            // --- Čuvanje u Bazi ---
            try
            {
                _context.Inventories.Add(newInventory);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully created Inventory record ID {InventoryId} for Product {ProductId}, Store {StoreId}.",
                                       newInventory.Id, newInventory.ProductId, newInventory.StoreId);

                // --- Mapiranje u DTO za povratak ---
                string? productName = await _catalogContext.Products
                                                .Where(p => p.Id == newInventory.ProductId)
                                                .Select(p => p.Name)
                                                .FirstOrDefaultAsync();
                return new InventoryDto
                {
                    Id = newInventory.Id,
                    ProductId = newInventory.ProductId,
                    StoreId = newInventory.StoreId,
                    Quantity = newInventory.Quantity,
                    OutOfStock = newInventory.OutOfStock,
                    LastUpdated = newInventory.LastUpdated,
                    ProductName = productName
                };
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating inventory record for Product {ProductId}, Store {StoreId}.", createDto.ProductId, targetStoreId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Generic error creating inventory record for Product {ProductId}, Store {StoreId}.", createDto.ProductId, targetStoreId);
                throw;
            }
        }

        public async Task<IEnumerable<InventoryDto>> GetInventoryAsync(string requestingUserId, bool isAdmin, int? productId = null, int? storeId = null)
        {
            _logger.LogInformation("Attempting to get inventory by User {UserId} (IsAdmin: {IsAdmin}). Filters - ProductId: {ProductId}, StoreId: {StoreId}",
                                   requestingUserId, isAdmin, productId?.ToString() ?? "N/A", storeId?.ToString() ?? "N/A");

            // --- Autorizacija i određivanje ciljanog StoreId ---
            int? targetStoreId = storeId;

            if (!isAdmin)
            {
                var sellerUser = await _userManager.FindByIdAsync(requestingUserId);
                if (!sellerUser.StoreId.HasValue)
                {
                    _logger.LogWarning("Seller {UserId} attempted to get inventory but has no StoreId assigned.", requestingUserId);
                    return Enumerable.Empty<InventoryDto>();
                }

                if (targetStoreId.HasValue && targetStoreId.Value != sellerUser.StoreId.Value)
                {
                    _logger.LogWarning("Seller {UserId} attempted to filter inventory for StoreId {FilterStoreId}, but owns StoreId {OwnedStoreId}. Forcing own StoreId.",
                                       requestingUserId, targetStoreId.Value, sellerUser.StoreId.Value);
                    targetStoreId = sellerUser.StoreId.Value;
                }
                else if (!targetStoreId.HasValue)
                {
                    targetStoreId = sellerUser.StoreId.Value;
                    _logger.LogInformation("Seller {UserId} getting inventory for their StoreId {StoreId}.", requestingUserId, targetStoreId);
                }
            }

            var query = _context.Inventories
                              .AsNoTracking();

            if (productId.HasValue)
            {
                query = query.Where(inv => inv.ProductId == productId.Value);
                _logger.LogInformation("Applying filter: ProductId = {ProductId}", productId.Value);
            }

            if (targetStoreId.HasValue)
            {
                query = query.Where(inv => inv.StoreId == targetStoreId.Value);
                _logger.LogInformation("Applying filter: StoreId = {StoreId}", targetStoreId.Value);
            }

            // --- Izvršavanje Upita za Inventory ---
            List<Models.Inventory> inventoryList;
            try
            {
                inventoryList = await query.ToListAsync();
                _logger.LogInformation("Retrieved {Count} inventory records matching criteria.", inventoryList.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving inventory records for User {UserId} with filters ProductId={ProductId}, StoreId={StoreId}.",
                              requestingUserId, productId?.ToString() ?? "N/A", storeId?.ToString() ?? "N/A");
                throw;
            }

            if (!inventoryList.Any())
            {
                return Enumerable.Empty<InventoryDto>();
            }
            var productIds = inventoryList.Select(inv => inv.ProductId).Distinct().ToList();

            Dictionary<int, string> productNames = new Dictionary<int, string>();
            try
            {
                productNames = await _catalogContext.Products
                                       .Where(p => productIds.Contains(p.Id))
                                       .AsNoTracking()
                                       .Select(p => new { p.Id, p.Name })
                                       .ToDictionaryAsync(p => p.Id, p => p.Name ?? "N/A");
                _logger.LogInformation("Retrieved names for {Count} products.", productNames.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product names for inventory list.");
            }

            // --- Mapiranje u DTO ---
            var dtoList = inventoryList.Select(inv => new InventoryDto
            {
                Id = inv.Id,
                ProductId = inv.ProductId,
                StoreId = inv.StoreId,
                Quantity = inv.Quantity,
                OutOfStock = inv.OutOfStock,
                LastUpdated = inv.LastUpdated,
                ProductName = productNames.TryGetValue(inv.ProductId, out var name) ? name : "N/A"
            }).ToList();

            return dtoList;
        }

        public Task<InventoryDto?> UpdateQuantityAsync(string requestingUserId, bool isAdmin, UpdateInventoryQuantityRequestDto updateDto)
        {
            throw new NotImplementedException();
        }

        public Task<bool> DeleteInventoryAsync(string requestingUserId, bool isAdmin, int inventoryId)
        {
            throw new NotImplementedException();
        }
    }
}