using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Catalog.Models;
using Inventory.Dtos;
using Inventory.Interfaces;
using Inventory.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notifications.Interfaces;
using Notifications.Services;
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
        private readonly INotificationService _dbNotificationService;
        private readonly IPushNotificationService _pushNotificationService;

        public InventoryService(
            InventoryDbContext context,
            UserManager<User> userManager,
            ILogger<InventoryService> logger,
            CatalogDbContext catalogContext,
            StoreDbContext storeContext,
            INotificationService dbNotificationService,
            IPushNotificationService pushNotificationService
            )
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _catalogContext = catalogContext ?? throw new ArgumentNullException(nameof(catalogContext));
            _storeContext = storeContext ?? throw new ArgumentNullException(nameof(storeContext));
            _dbNotificationService = dbNotificationService ?? throw new ArgumentNullException(nameof(dbNotificationService));
            _pushNotificationService = pushNotificationService ?? throw new ArgumentNullException(nameof(pushNotificationService));
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

        public async Task<InventoryDto?> UpdateQuantityAsync(string requestingUserId, bool isAdmin, UpdateInventoryQuantityRequestDto updateDto)
        {
            _logger.LogInformation("Attempting to update inventory quantity by User {UserId} (IsAdmin: {IsAdmin}). DTO: {@UpdateDto}",
                                   requestingUserId, isAdmin, updateDto);

            // --- Validacija DTO ---
            if (updateDto == null) throw new ArgumentNullException(nameof(updateDto));
            if (updateDto.ProductId <= 0) throw new ArgumentException("Invalid ProductId.", nameof(updateDto.ProductId));
            if (updateDto.StoreId <= 0) throw new ArgumentException("Invalid StoreId.", nameof(updateDto.StoreId));
            if (updateDto.NewQuantity < 0) throw new ArgumentOutOfRangeException(nameof(updateDto.NewQuantity), "Quantity cannot be negative.");

            // --- Autorizacija i Određivanje StoreId ---
            int targetStoreId = updateDto.StoreId;
            User? sellerUser = null;

            if (!isAdmin)
            {
                sellerUser = await _userManager.FindByIdAsync(requestingUserId);
                if (sellerUser == null) throw new KeyNotFoundException("Requesting user not found.");
                if (!sellerUser.StoreId.HasValue)
                {
                    _logger.LogWarning("Seller {UserId} attempted to update inventory but has no StoreId assigned.", requestingUserId);
                    throw new UnauthorizedAccessException("Seller does not have an associated store.");
                }
                if (targetStoreId != sellerUser.StoreId.Value)
                {
                    _logger.LogWarning("Seller {UserId} attempted to update inventory for StoreId {TargetStoreId}, but owns StoreId {OwnedStoreId}. Denying access.",
                                      requestingUserId, targetStoreId, sellerUser.StoreId.Value);
                    throw new UnauthorizedAccessException("Seller cannot update inventory for another store.");
                }
                _logger.LogInformation("Seller {UserId} updating inventory for their StoreId {StoreId}.", requestingUserId, targetStoreId);
            }
            else
            {
                _logger.LogInformation("Admin {UserId} updating inventory for StoreId {StoreId} from DTO.", requestingUserId, targetStoreId);
                sellerUser = await _userManager.Users.FirstOrDefaultAsync(u => u.StoreId == targetStoreId);
                if (sellerUser == null) _logger.LogWarning("Could not find Seller associated with StoreId {StoreId} for potential notifications.", targetStoreId);
            }

            // --- Pronalaženje Postojećeg Zapisa ---
            var inventoryItem = await _context.Inventories
                                            .FirstOrDefaultAsync(inv => inv.ProductId == updateDto.ProductId && inv.StoreId == targetStoreId);

            if (inventoryItem == null)
            {
                _logger.LogWarning("Inventory update failed: Record not found for ProductId {ProductId} and StoreId {StoreId}.", updateDto.ProductId, targetStoreId);
                return null;
            }

            // --- Logika Ažuriranja ---
            bool wasOutOfStock = inventoryItem.OutOfStock;
            int oldQuantity = inventoryItem.Quantity;

            inventoryItem.Quantity = updateDto.NewQuantity;
            inventoryItem.OutOfStock = inventoryItem.Quantity <= 0;
            inventoryItem.LastUpdated = DateTime.UtcNow;

            _context.Entry(inventoryItem).State = EntityState.Modified;

            // --- Čuvanje i Notifikacije ---
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully updated Inventory record ID {InventoryId} (Product {ProductId}, Store {StoreId}) quantity from {OldQty} to {NewQty}. OutOfStock: {OutOfStock}",
                                       inventoryItem.Id, inventoryItem.ProductId, inventoryItem.StoreId, oldQuantity, inventoryItem.Quantity, inventoryItem.OutOfStock);

                // --- PROVJERA I SLANJE NOTIFIKACIJE ---
                if (!wasOutOfStock && inventoryItem.OutOfStock)
                {
                    _logger.LogInformation("Product {ProductId} in Store {StoreId} became OutOfStock. Initiating notifications.", inventoryItem.ProductId, inventoryItem.StoreId);

                    string? productName = await _catalogContext.Products
                                                    .Where(p => p.Id == inventoryItem.ProductId)
                                                    .Select(p => p.Name)
                                                    .FirstOrDefaultAsync();

                    if (sellerUser != null)
                    {
                        string message = $"Proizvod '{productName ?? "N/A"}' (ID: {inventoryItem.ProductId}) u vašoj prodavnici je ostao bez zaliha!";

                        // Kreiraj DB Notifikaciju
                        await _dbNotificationService.CreateNotificationAsync(
                            sellerUser.Id,
                            message,
                            inventoryItem.ProductId
                        );

                        // Pošalji Push Notifikaciju
                        if (!string.IsNullOrWhiteSpace(sellerUser.FcmDeviceToken))
                        {
                            string pushTitle = "Proizvod Van Zaliha!";
                            var pushData = new Dictionary<string, string> {
                                 { "productId", inventoryItem.ProductId.ToString() },
                                 { "storeId", inventoryItem.StoreId.ToString() },
                                 { "screen", "InventoryDetail" }
                             };

                            Task<bool> pushTask;

                            pushTask = _pushNotificationService.SendPushNotificationAsync(sellerUser.FcmDeviceToken, pushTitle, message, pushData);

                            try { await pushTask; } catch (Exception ex) { _logger.LogError(ex, "Error sending OutOfStock push notification."); }
                        }
                        else
                        {
                            _logger.LogWarning("Seller {SellerId} has no device token for OutOfStock notification.", sellerUser.Id);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Could not find Seller for StoreId {StoreId} to send OutOfStock notification for Product {ProductId}.", inventoryItem.StoreId, inventoryItem.ProductId);
                    }
                }

                // --- Mapiranje u DTO za povratak ---
                string? finalProductName = await _catalogContext.Products
                                                  .Where(p => p.Id == inventoryItem.ProductId)
                                                  .Select(p => p.Name)
                                                  .FirstOrDefaultAsync();
                return new InventoryDto
                {
                    Id = inventoryItem.Id,
                    ProductId = inventoryItem.ProductId,
                    StoreId = inventoryItem.StoreId,
                    Quantity = inventoryItem.Quantity,
                    OutOfStock = inventoryItem.OutOfStock,
                    LastUpdated = inventoryItem.LastUpdated,
                    ProductName = finalProductName ?? "N/A"
                };
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict updating inventory for Product {ProductId}, Store {StoreId}.", updateDto.ProductId, targetStoreId);
                return null;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error updating inventory for Product {ProductId}, Store {StoreId}.", updateDto.ProductId, targetStoreId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Generic error updating inventory for Product {ProductId}, Store {StoreId}.", updateDto.ProductId, targetStoreId);
                throw;
            }
        }
        public Task<bool> DeleteInventoryAsync(string requestingUserId, bool isAdmin, int inventoryId)
        {
            throw new NotImplementedException();
        }
    }
}