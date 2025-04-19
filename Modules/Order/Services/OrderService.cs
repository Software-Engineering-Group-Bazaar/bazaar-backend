using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging; // Make sure you have this using for ILogger
using Order.Interface;
using Order.Models;

namespace Order.Services
{
    // Helper record defined here or elsewhere
    public record OrderItemInput(int ProductId, int Quantity, decimal Price);

    public class OrderService : IOrderService
    {
        private readonly OrdersDbContext _context;
        private readonly ILogger<OrderService> _logger;

        public OrderService(OrdersDbContext context, ILogger<OrderService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<OrderModel> CreateOrderAsync(string buyerId, int storeId)
        {
            // --- Validation ---
            if (storeId <= 0) // Basic validation
            {
                throw new ArgumentException("Invalid StoreId provided.", nameof(storeId));
            }
            // --- Create Order Header ---
            var order = new OrderModel
            {
                BuyerId = buyerId,
                StoreId = storeId,
                Status = OrderStatus.Requested,
                Time = DateTime.UtcNow,
            };

            _context.Orders.Add(order);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully created Order {OrderId} for Buyer {BuyerId}, Store {StoreId}", order.Id, buyerId, storeId);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating order for Buyer {BuyerId}, Store {StoreId}", buyerId, storeId);
                throw;
            }
            return order;
        }

        public async Task<OrderModel?> GetOrderByIdAsync(int orderId)
        {
            _logger.LogDebug("Fetching Order with Id {OrderId}", orderId);
            return await _context.Orders
                                 .Include(o => o.OrderItems)
                                 .AsNoTracking()
                                 .FirstOrDefaultAsync(o => o.Id == orderId);
        }

        public async Task<IEnumerable<OrderModel>> GetAllOrdersAsync()
        {
            _logger.LogDebug("Fetching all orders");
            return await _context.Orders
                                 .Include(o => o.OrderItems)
                                 .AsNoTracking()
                                 .ToListAsync();
        }

        public async Task<IEnumerable<OrderModel>> GetOrdersByBuyerAsync(string buyerId)
        {
            _logger.LogDebug("Fetching orders for BuyerId {BuyerId}", buyerId);
            return await _context.Orders
                                 .Include(o => o.OrderItems)
                                 .Where(o => o.BuyerId == buyerId)
                                 .AsNoTracking()
                                 .ToListAsync();
        }

        public async Task<IEnumerable<OrderModel>> GetOrdersByStoreAsync(int storeId)
        {
            _logger.LogDebug("Fetching orders for StoreId {StoreId}", storeId);
            return await _context.Orders
                                .Include(o => o.OrderItems)
                                .Where(o => o.StoreId == storeId)
                                .AsNoTracking()
                                .ToListAsync();
        }


        public async Task<bool> UpdateOrderStatusAsync(int orderId, OrderStatus newStatus)
        {
            var order = await _context.Orders.FindAsync(orderId);

            if (order == null)
            {
                _logger.LogWarning("UpdateOrderStatus failed: Order with Id {OrderId} not found.", orderId);
                return false;
            }

            order.Status = newStatus;
            _context.Entry(order).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated status for OrderId {OrderId} to {Status}", orderId, newStatus);
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict updating status for OrderId {OrderId}", orderId);
                return false;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error updating status for OrderId {OrderId}", orderId);
                return false;
            }
        }

        public async Task<bool> DeleteOrderAsync(int orderId)
        {
            var order = await _context.Orders
                                      .Include(o => o.OrderItems)
                                      .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                _logger.LogWarning("DeleteOrder failed: Order with Id {OrderId} not found.", orderId);
                return false;
            }

            _context.Orders.Remove(order); // EF Core handles cascade delete based on FK setup

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Deleted Order with Id {OrderId}", orderId);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error deleting order {OrderId}", orderId);
                return false;
            }
        }

        public Task<bool> UpdateOrderAsync(int id, string? buyerId, string? storeId, OrderStatus? status, DateTime? time, decimal? total)
        {
            throw new NotImplementedException();
        }
    }
}