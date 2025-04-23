using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Catalog.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging; // Make sure you have this using for ILogger
using Order.Interface;
using Order.Models;

namespace Order.Services
{
    public class OrderItemService : IOrderItemService
    {
        private readonly OrdersDbContext _context;
        private readonly CatalogDbContext _catalog;
        private readonly ILogger<OrderItemService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        public OrderItemService(OrdersDbContext context, ILogger<OrderItemService> logger, CatalogDbContext catalog, IServiceScopeFactory scopeFactory)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _scopeFactory = scopeFactory;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private decimal CalculateOrderTotal(IEnumerable<OrderItem> items)
        {
            if (items == null) return 0m;
            // Change tracker ensures this uses current values of tracked items
            return items.Sum(item => item.Price * item.Quantity);
        }

        public async Task<OrderItem> CreateOrderItemAsync(int orderId, int productId, int quantity) // Removed price parameter
        {
            // --- Validation ---
            if (orderId <= 0) throw new ArgumentException("OrderItem must have a valid OrderId.", nameof(orderId));
            if (productId <= 0) throw new ArgumentException("Invalid ProductId.", nameof(productId));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");

            // --- Find Product and Price ---
            // Fetch the product to get its price and validate existence/status
            var product = await _catalog.Products.FindAsync(productId);
            if (product == null)
            {
                throw new ArgumentException($"Product with Id {productId} not found.", nameof(productId));
            }
            if (!product.IsActive)
            {
                throw new ArgumentException($"Product with Id {productId} is not active.", nameof(productId));
            }

            decimal itemPrice = product.RetailPrice;
            if (product.WholesaleThreshold != null && product.WholesalePrice != null && quantity >= product.WholesaleThreshold)
            {
                itemPrice = (decimal)product.WholesalePrice;
            }
            // --- Find Parent Order (Tracked) ---
            var parentOrder = await _context.Orders
                                            .Include(o => o.OrderItems)
                                            .FirstOrDefaultAsync(o => o.Id == orderId);
            if (parentOrder == null)
            {
                _logger.LogError("Cannot create OrderItem. Parent Order with Id {OrderId} not found.", orderId);
                throw new InvalidOperationException($"Order with Id {orderId} not found.");
            }

            // --- Create Item with fetched Price ---
            var newOrderItem = new OrderItem
            {
                ProductId = productId,
                Quantity = quantity,
                Price = itemPrice // Set the price fetched from the Product
                // OrderId/Order navigation property handled by EF Core via parentOrder.OrderItems.Add
            };

            // --- Add Item & Update Order ---
            parentOrder.OrderItems.Add(newOrderItem);
            parentOrder.Total = CalculateOrderTotal(parentOrder.OrderItems);
            _context.Entry(parentOrder).State = EntityState.Modified;

            // --- Save Changes ---
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Created OrderItem {OrderItemId} (Product {ProductId}, Price {Price}) for Order {OrderId} and updated Order Total.", newOrderItem.Id, productId, itemPrice, orderId);
                return newOrderItem;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error creating OrderItem (Product {ProductId}) for Order {OrderId} and updating Total.", productId, orderId);
                throw;
            }
        }
        public async Task<bool> UpdateOrderItemAsync(int orderItemId, int quantity, int productId)
        {
            // --- Validation ---
            if (orderItemId <= 0) throw new ArgumentException("Invalid OrderItemId provided.", nameof(orderItemId));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
            if (productId <= 0) throw new ArgumentException("Invalid ProductId provided.", nameof(productId));

            // --- Find Existing Item (Tracked) ---
            var existingItem = await _context.OrderItems.FindAsync(orderItemId);
            if (existingItem == null)
            {
                _logger.LogWarning("UpdateOrderItem failed: Item with Id {OrderItemId} not found.", orderItemId);
                return false;
            }

            // --- Find Product and Price ---
            // Fetch the product for the potentially new ProductId
            var product = await _catalog.Products.FindAsync(productId);
            if (product == null)
            {
                throw new ArgumentException($"Product with Id {productId} not found.", nameof(productId));
            }
            if (!product.IsActive)
            {
                throw new ArgumentException($"Product with Id {productId} is not active.", nameof(productId));
            }

            decimal newItemPrice = product.RetailPrice;
            if (product.WholesaleThreshold != null && product.WholesalePrice != null && quantity >= product.WholesaleThreshold)
            {
                newItemPrice = (decimal)product.WholesalePrice;
            }

            var parentOrder = await _context.Orders
                                           .Include(o => o.OrderItems)
                                           .FirstOrDefaultAsync(o => o.Id == existingItem.OrderId);
            if (parentOrder == null)
            {
                _logger.LogError("Data Consistency Error: OrderItem {OrderItemId} exists, but parent Order {OrderId} not found.", orderItemId, existingItem.OrderId);
                return false;
            }

            // --- Apply Updates to Item ---
            bool priceChanged = existingItem.Price != newItemPrice;
            bool quantityChanged = existingItem.Quantity != quantity;
            bool productChanged = existingItem.ProductId != productId;

            existingItem.Quantity = quantity;
            existingItem.ProductId = productId; // Update product if changed
            existingItem.Price = newItemPrice; // Update price based on product/wholesale logic

            // --- Recalculate and Update Order Total ---
            // Recalculate if quantity, price, or product changed, or just always recalculate for simplicity
            if (quantityChanged || priceChanged || productChanged) // Optimization: only recalc if relevant data changed
            {
                parentOrder.Total = CalculateOrderTotal(parentOrder.OrderItems);
                _context.Entry(parentOrder).State = EntityState.Modified; // Mark order as modified only if total might change
                _logger.LogInformation("Recalculating total for Order {OrderId} due to item {OrderItemId} update.", parentOrder.Id, orderItemId);
            }


            // --- Save Changes ---
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated OrderItem {OrderItemId} (Product {ProductId}, Quantity {Quantity}, Price {Price}) and Order {OrderId} Total if necessary.",
                    orderItemId, productId, quantity, newItemPrice, parentOrder.Id);
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict updating OrderItem {OrderItemId} or Order {OrderId}", orderItemId, parentOrder.Id);
                return false;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error updating OrderItem {OrderItemId} (Product {ProductId}) or Order {OrderId} Total", orderItemId, productId, parentOrder.Id);
                return false;
            }
        }

        // --- Keep other methods as they were ---
        public async Task<OrderItem?> GetOrderItemByIdAsync(int orderItemId)
        {
            _logger.LogDebug("Fetching OrderItem with Id {OrderItemId}", orderItemId);
            return await _context.OrderItems
                                 .AsNoTracking()
                                 .FirstOrDefaultAsync(oi => oi.Id == orderItemId);
        }

        public async Task<IEnumerable<OrderItem>> GetOrderItemsByOrderIdAsync(int orderId)
        {
            _logger.LogDebug("Fetching OrderItems for OrderId {OrderId}", orderId);
            return await _context.OrderItems
                                 .Where(oi => oi.OrderId == orderId)
                                 .AsNoTracking()
                                 .ToListAsync();
        }



        public async Task<bool> DeleteOrderItemAsync(int orderItemId)
        {
            if (orderItemId <= 0) throw new ArgumentException("Invalid OrderItemId provided.", nameof(orderItemId));
            var itemToDelete = await _context.OrderItems.FindAsync(orderItemId);
            if (itemToDelete == null)
            {
                _logger.LogWarning("DeleteOrderItem failed: Item with Id {OrderItemId} not found.", orderItemId);
                return false;
            }

            var parentOrder = await _context.Orders
                                            .Include(o => o.OrderItems)
                                            .FirstOrDefaultAsync(o => o.Id == itemToDelete.OrderId);
            if (parentOrder == null)
            {
                _logger.LogError("Data Consistency Error: OrderItem {OrderItemId} exists, but parent Order {OrderId} not found.", orderItemId, itemToDelete.OrderId);
                // Decide how to handle - delete the orphaned item anyway?
                _context.OrderItems.Remove(itemToDelete);
                await _context.SaveChangesAsync();
                return false; // Indicate failure due to inconsistency
            }

            _context.OrderItems.Remove(itemToDelete); // Mark item for deletion
            // Recalculate total based on remaining items
            parentOrder.Total = CalculateOrderTotal(parentOrder.OrderItems);
            _context.Entry(parentOrder).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync(); // Deletes item, updates total
                _logger.LogInformation("Deleted OrderItem {OrderItemId} and updated Total for Order {OrderId}", orderItemId, parentOrder.Id);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error deleting OrderItem {OrderItemId} or updating Order {OrderId} Total", orderItemId, parentOrder.Id);
                return false;
            }
        }

        public async Task<bool> CheckValid(int id, int quantity, int productId, decimal price)
        {
            if (id <= 0) return false;
            if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
            if (productId < 0) throw new ArgumentException("Invalid ProductId provided.", nameof(productId));
            if (price < 0) throw new ArgumentException(nameof(price), "Price must be positive.");
            // --- Find Existing Item (Tracked) ---
            var existingItem = await _context.OrderItems.FindAsync(id);
            if (existingItem == null)
            {
                _logger.LogWarning("UpdateOrderItem failed: Item with Id {OrderItemId} not found.", id);
                return false;
            }
            var product = await _catalog.Products.FindAsync(productId);
            if (product == null)
            {
                throw new ArgumentException($"Product with Id {productId} not found.", nameof(productId));
            }
            if (!product.IsActive)
            {
                throw new ArgumentException($"Product with Id {productId} is not active.", nameof(productId));
            }
            return true;
        }

        public async Task<bool> ForceUpdateOrderItemAsync(int id, int quantity, int productId, decimal price)
        {
            // Thread safe
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
                var existingItem = await dbContext.OrderItems.FindAsync(id);
                if (existingItem is null)
                    throw new InvalidOperationException("item does not exist");
                bool priceChanged = existingItem.Price != price;
                bool quantityChanged = existingItem.Quantity != quantity;
                bool productChanged = existingItem.ProductId != productId;

                existingItem.Quantity = quantity;
                existingItem.ProductId = productId; // Update product if changed
                existingItem.Price = price; // Update price based on product/wholesale logic
                var parentOrder = await dbContext.Orders
                                                           .Include(o => o.OrderItems)
                                                           .FirstOrDefaultAsync(o => o.Id == existingItem.OrderId);
                if (parentOrder == null)
                {
                    _logger.LogError("Data Consistency Error: OrderItem {OrderItemId} exists, but parent Order {OrderId} not found.", id, existingItem.OrderId);
                    throw new InvalidOperationException("order does not exist");
                }
                // --- Recalculate and Update Order Total ---
                // Recalculate if quantity, price, or product changed, or just always recalculate for simplicity
                if (quantityChanged || priceChanged || productChanged) // Optimization: only recalc if relevant data changed
                {
                    parentOrder.Total = CalculateOrderTotal(parentOrder.OrderItems);
                    //_context.Entry(parentOrder).State = EntityState.Modified; // Mark order as modified only if total might change
                    _logger.LogInformation("Recalculating total for Order {OrderId} due to item {OrderItemId} update.", parentOrder.Id, id);
                }


                // --- Save Changes ---


                await dbContext.SaveChangesAsync();
                _logger.LogInformation("Updated OrderItem {OrderItemId} (Product {ProductId}, Quantity {Quantity}, Price {Price}) and Order {OrderId} Total if necessary.",
                    id, productId, quantity, price, parentOrder.Id);
                return true;

            }
        }
    }
}