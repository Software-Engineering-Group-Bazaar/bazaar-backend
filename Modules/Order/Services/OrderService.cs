using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory.Interfaces;
using Inventory.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging; // Make sure you have this using for ILogger
using Order.Interface;
using Order.Models;
using Store.Models;
using Users.Interfaces;
using Users.Models;

namespace Order.Services
{
    // Helper record defined here or elsewhere
    public record OrderItemInput(int ProductId, int Quantity, decimal Price);

    public class OrderService : IOrderService
    {
        private readonly OrdersDbContext _context;
        private readonly InventoryDbContext _inventoryContext;
        private readonly StoreDbContext _storeDbContext;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<OrderService> _logger;
        private readonly IAddressService _addressService;

        public OrderService(OrdersDbContext context,
                            InventoryDbContext inventoryContext,
                            StoreDbContext storeDbContext,
                            UserManager<User> userManager,
                            ILogger<OrderService> logger,
                            IAddressService addressService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _inventoryContext = inventoryContext;
            _storeDbContext = storeDbContext;
            _userManager = userManager;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _addressService = addressService ?? throw new ArgumentNullException(nameof(addressService));
        }

        public async Task<OrderModel> CreateOrderAsync(string buyerId, int storeId, int addressId = 0)
        {
            // --- Validation ---
            if (storeId <= 0) // Basic validation
            {
                throw new ArgumentException("Invalid StoreId provided.", nameof(storeId));
            }

            var address = await _addressService.GetAddressByIdAsync(addressId);

            if (addressId != 0 && address == null)
            {
                _logger.LogError($"Invalid AddressId provided: {addressId}");
                throw new ArgumentException("Invalid AddressId provided.", nameof(addressId));
            }

            // --- Create Order Header ---
            var order = new OrderModel
            {
                BuyerId = buyerId,
                StoreId = storeId,
                Status = OrderStatus.Requested,
                Time = DateTime.UtcNow,
                AddressId = addressId
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


        public async Task<bool> UpdateOrderStatusAsync(int orderId, OrderStatus newStatus, bool adminDelivery = false, int estimatedPreparationTimeInMinutes = 0)
        {
            var order = await _context.Orders.FindAsync(orderId);

            if (order == null)
            {
                _logger.LogWarning("UpdateOrderStatus failed: Order with Id {OrderId} not found.", orderId);
                return false;
            }

            order.Status = newStatus;
            if (newStatus == OrderStatus.Confirmed)
            {
                order.AdminDelivery = adminDelivery;

                DateTime nowUtc = DateTime.UtcNow;
                TimeSpan preparationTime = TimeSpan.FromMinutes(estimatedPreparationTimeInMinutes);
                DateTime estimatedReadyAt = nowUtc.Add(preparationTime);

                order.ExpectedReadyAt = estimatedReadyAt;
            }
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

        public async Task<bool> UpdateOrderAsync(int id, string? buyerId, int? storeId, OrderStatus? status, DateTime? time, decimal? total)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id);
            if (order is null)
            {
                throw new InvalidDataException("ne postoji taj id");
            }
            if (buyerId != null)
            {
                var buyer = await _userManager.FindByIdAsync(buyerId);
                if (buyer is null)
                    throw new InvalidDataException("ne postoji taj kupac");
            }
            if (storeId != null)
            {
                var store = await _storeDbContext.Stores.FirstOrDefaultAsync(s => s.id == storeId);
                if (store is null)
                    throw new InvalidDataException("ne postoji ta prodavnica");
            }

            if (buyerId != null && order.BuyerId != buyerId) order.BuyerId = buyerId;
            if (storeId != null && order.StoreId != storeId) order.StoreId = (int)storeId;
            if (status != null && order.Status != status) order.Status = (OrderStatus)status;
            if (time != null && order.Time != time) order.Time = (DateTime)time;
            if (total != null && order.Total != total) order.Total = total;
            //_context.Orders.Update(order);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}