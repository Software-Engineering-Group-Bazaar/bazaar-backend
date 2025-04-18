using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Catalog.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging; // Make sure you have this using for ILogger
using Order.DTOs;
using Order.Interface;
using Order.Models;
using Store.Models;
using Users.Models;

namespace Order.Services
{
    // Helper record defined here or elsewhere
    public record OrderItemInput(int ProductId, int Quantity, decimal Price);

    public class OrderService : IOrderService
    {
        private readonly OrdersDbContext _context;
        private readonly UserManager<User> _userManager;

        private readonly CatalogDbContext _catalogContext;

        private readonly ILogger<OrderService> _logger;

        public OrderService(OrdersDbContext ordersContext, UserManager<User> userManager, CatalogDbContext catalogContext, ILogger<OrderService> logger)
        {
            _context = ordersContext;
            _userManager = userManager;
            _catalogContext = catalogContext;
            _logger = logger;
        }
        public async Task<OrderModel> CreateOrderAsync(int buyerId, int storeId)
        {
            // --- Validation ---
            if (buyerId <= 0) // Basic validation
            {
                throw new ArgumentException("Invalid BuyerId provided.", nameof(buyerId));
            }
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

        public async Task<IEnumerable<OrderModel>> GetOrdersByBuyerAsync(int buyerId)
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



        public async Task<IEnumerable<OrderSummaryDto>> GetOrdersForSellerAsync(string sellerUserId)
        {
            _logger.LogInformation("Dohvatanje narudžbi za sellerUserId {SellerUserId}", sellerUserId);

            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == sellerUserId);

            if (user == null || user.StoreId == null)
            {
                _logger.LogWarning("Korisnik nije pronađen ili nema StoreId: {SellerUserId}", sellerUserId);
                return Enumerable.Empty<OrderSummaryDto>();
            }

            int storeId = user.StoreId.Value;

            var orders = await _context.Orders
                .Where(o => o.StoreId == storeId)
                .Select(o => new OrderSummaryDto
                {
                    OrderId = o.Id,
                    OrderDate = o.Time,
                    TotalAmount = o.Total ?? 0
                })
                .ToListAsync();

            _logger.LogInformation("Vraćeno {Count} narudžbi za StoreId {StoreId}", orders.Count, storeId);

            return orders;
        }


        public async Task<bool> UpdateOrderStatusForSellerAsync(string sellerUserId, UpdateOrderStatusRequestDto updateDto)
        {
            if (updateDto == null) throw new ArgumentNullException(nameof(updateDto));
            if (string.IsNullOrWhiteSpace(sellerUserId)) throw new ArgumentNullException(nameof(sellerUserId));
            if (updateDto.OrderId <= 0) throw new ArgumentException("Invalid Order ID.", nameof(updateDto.OrderId));
            if (string.IsNullOrWhiteSpace(updateDto.NewStatus)) throw new ArgumentException("New status is required.", nameof(updateDto.NewStatus));

            _logger.LogInformation("Attempting to update status for Order {OrderId} to {NewStatus} by User {UserId}",
                updateDto.OrderId, updateDto.NewStatus, sellerUserId);

            // 1. Provjeri vlasništvo Sellera nad prodavnicom ove narudžbe
            var sellerUser = await _userManager.FindByIdAsync(sellerUserId);
            if (sellerUser == null || !sellerUser.StoreId.HasValue)
            {
                _logger.LogWarning("UpdateOrderStatus: Seller {UserId} not found or does not have an associated store.", sellerUserId);
                // Baci izuzetak da kontroler vrati 403 Forbidden ili 404 NotFound ovisno o logici
                throw new UnauthorizedAccessException("Seller is not authorized or does not own a store.");
            }
            int sellerStoreId = sellerUser.StoreId.Value;

            // 2. Dohvati narudžbu UKLJUČUJUĆI trenutni status
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == updateDto.OrderId);
            if (order == null)
            {
                _logger.LogWarning("UpdateOrderStatus: Order {OrderId} not found.", updateDto.OrderId);
                return false; // Vrati false da kontroler može vratiti 404 Not Found
            }

            // 3. Verifikuj da narudžba pripada prodavnici ovog Sellera
            if (order.StoreId != sellerStoreId)
            {
                _logger.LogWarning("Forbidden attempt by User {UserId} (Store {SellerStoreId}) to update status for Order {OrderId} belonging to Store {OrderStoreId}.",
                    sellerUserId, sellerStoreId, updateDto.OrderId, order.StoreId);
                // Baci izuzetak da kontroler vrati 403 Forbidden
                throw new UnauthorizedAccessException("Seller is not authorized to update status for this order.");
            }

            // 4. Validacija Novog Statusa (string u enum)
            if (!Enum.TryParse<OrderStatus>(updateDto.NewStatus, true, out var newStatusEnum)) // true za case-insensitive
            {
                _logger.LogWarning("UpdateOrderStatus: Invalid NewStatus value '{NewStatus}' provided for Order {OrderId}.", updateDto.NewStatus, updateDto.OrderId);
                throw new ArgumentException($"Invalid status value: {updateDto.NewStatus}. Valid statuses are: {string.Join(", ", Enum.GetNames(typeof(OrderStatus)))}");
            }

            // 5. Validacija Tranzicije Statusa (Implementiraj logiku prema pravilima)
            if (!IsValidStatusTransition(order.Status, newStatusEnum))
            {
                _logger.LogWarning("UpdateOrderStatus: Invalid status transition from {CurrentStatus} to {NewStatus} for Order {OrderId}.",
                                   order.Status, newStatusEnum, updateDto.OrderId);
                throw new InvalidOperationException($"Cannot change order status from {order.Status} to {newStatusEnum}.");
            }

            // 6. Ažuriraj Status
            order.Status = newStatusEnum;
            _context.Entry(order).State = EntityState.Modified;

            // 7. Sačuvaj Promjene
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully updated status for Order {OrderId} to {NewStatus} by User {UserId}",
                                       updateDto.OrderId, newStatusEnum, sellerUserId);

                // TODO: Ovdje pozvati INotificationService ako/kada bude implementiran
                // await _notificationService.SendOrderStatusUpdateAsync(order, sellerUser); 

                return true; // Uspjeh
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict updating status for OrderId {OrderId}", updateDto.OrderId);
                // Provjeri da li order još postoji
                if (!await _context.Orders.AnyAsync(o => o.Id == updateDto.OrderId)) return false; // Više ne postoji
                else throw; // Baci originalnu grešku
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error updating status for OrderId {OrderId}", updateDto.OrderId);
                // Možda vratiti false ili baciti izuzetak
                return false;
            }
        }

        // Privatna helper metoda za validaciju tranzicije statusa
        private bool IsValidStatusTransition(OrderStatus currentStatus, OrderStatus newStatus)
        {
            // Implementiraj pravila iz issue-a
            switch (currentStatus)
            {
                case OrderStatus.Requested:
                    return newStatus == OrderStatus.Confirmed || newStatus == OrderStatus.Rejected || newStatus == OrderStatus.Cancelled;
                case OrderStatus.Confirmed:
                    return newStatus == OrderStatus.Ready || newStatus == OrderStatus.Sent || newStatus == OrderStatus.Cancelled;
                case OrderStatus.Ready:
                    return newStatus == OrderStatus.Delivered || newStatus == OrderStatus.Cancelled;
                case OrderStatus.Sent:
                    return newStatus == OrderStatus.Delivered || newStatus == OrderStatus.Cancelled;
                // Finalni statusi - ne mogu se mijenjati (osim možda Cancelled iz nekih?)
                case OrderStatus.Delivered:
                case OrderStatus.Rejected:
                case OrderStatus.Cancelled:
                    return false; // Ne može se promijeniti iz finalnog statusa
                default:
                    return false; // Nepoznat trenutni status
            }
        }

        public async Task<OrderDetailDto?> GetOrderDetailsForSellerAsync(string sellerUserId, int orderId)
        {
            if (orderId <= 0) throw new ArgumentException("Invalid Order ID.", nameof(orderId));
            if (string.IsNullOrWhiteSpace(sellerUserId)) throw new ArgumentNullException(nameof(sellerUserId));

            // 1. Pronađi StoreId za Sellera
            var sellerUser = await _userManager.FindByIdAsync(sellerUserId);
            if (sellerUser == null || !sellerUser.StoreId.HasValue)
            {
                _logger.LogWarning("GetOrderDetails: Seller {UserId} not found or does not have an associated store.", sellerUserId);
                throw new UnauthorizedAccessException("Seller is not authorized or does not own a store."); // Ili KeyNotFoundException?
            }
            int sellerStoreId = sellerUser.StoreId.Value;

            // 2. Dohvati narudžbu sa stavkama i PROIZVODIMA
            var order = await _context.Orders
                .Where(o => o.Id == orderId)
                .Include(o => o.OrderItems) // Uključi stavke narudžbe
                                            // Ne možemo direktno uključiti Proizvod ako je u drugom DbContextu!
                .FirstOrDefaultAsync();

            if (order == null)
            {
                _logger.LogWarning("GetOrderDetails: Order {OrderId} not found.", orderId);
                return null; // NotFound
            }

            // 3. Verifikuj da narudžba pripada prodavnici ovog Sellera
            if (order.StoreId != sellerStoreId)
            {
                _logger.LogWarning("Forbidden attempt by User {UserId} (Store {SellerStoreId}) to access Order {OrderId} belonging to Store {OrderStoreId}.",
                    sellerUserId, sellerStoreId, orderId, order.StoreId);
                throw new UnauthorizedAccessException("Seller is not authorized to access this order.");
            }

            // 4. Dohvati dodatne podatke (npr. Buyer info, Product info)
            // --- Dohvat informacija o kupcu ---
            OrderUserInfoDto? buyerInfoDto = null;
            var buyer = await _userManager.FindByIdAsync(order.BuyerId.ToString()); // Pazi na tip ID-ja! Možda ne treba ToString() ako je BuyerId string
            if (buyer != null)
            {
                buyerInfoDto = new OrderUserInfoDto { Id = buyer.Id, UserName = buyer.UserName, Email = buyer.Email };
            }

            // --- Dohvat informacija o proizvodima za stavke ---
            // Ovo je malo komplikovanije jer su Product entiteti u CatalogDbContext
            var productIds = order.OrderItems.Select(oi => oi.ProductId).Distinct().ToList();
            var products = await _catalogContext.Products
                                   .Where(p => productIds.Contains(p.Id))
                                   .Include(p => p.Pictures) // Učitaj slike ako treba URL
                                   .ToDictionaryAsync(p => p.Id); // Lakše za mapiranje kasnije

            // 5. Mapiraj u OrderDetailDto
            var orderDetailDto = new OrderDetailDto
            {
                Id = order.Id,
                OrderDate = order.Time,
                Status = order.Status.ToString(), // Enum u string
                TotalAmount = order.Total ?? 0m,
                StoreId = order.StoreId,
                BuyerInfo = buyerInfoDto,
                ShippingAddress = null, // TODO: Dodaj logiku za ShippingAddress ako postoji
                Items = order.OrderItems.Select(oi =>
                {
                    // Pronađi odgovarajući proizvod iz dictionary-ja
                    products.TryGetValue(oi.ProductId, out var product);
                    return new OrderItemDto // Mapiraj u OrderItemDto
                    {
                        Id = oi.Id,
                        ProductId = oi.ProductId,
                        ProductName = product?.Name ?? "Unknown Product",
                        Quantity = oi.Quantity,
                        PricePerProduct = oi.Price,
                        Subtotal = oi.Quantity * oi.Price,
                        ProductImageUrl = product?.Pictures?.FirstOrDefault()?.Url // URL prve slike?
                    };
                }).ToList()
            };

            return orderDetailDto;
        }
    }
}
