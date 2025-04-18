using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Catalog.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notifications.Interfaces;
using Notifications.Services;
using Order.DTOs;
using Order.Interface;
using Order.Models;
using Users.Models;

namespace Order.Services
{
    public class OrderService : IOrderService
    {
        private readonly OrdersDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly CatalogDbContext _catalogContext; // Za dohvat Producta kod detalja narudžbe
        private readonly INotificationService _dbNotificationService; // Servis za našu bazu
        private readonly FcmPushNotificationService _fcmPushNotificationService; // Servis za FCM Push
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            OrdersDbContext ordersContext,
            UserManager<User> userManager,
            CatalogDbContext catalogContext, // Dodajemo CatalogContext
            INotificationService dbNotificationService,
            FcmPushNotificationService fcmPushNotificationService, // Koristimo konkretan tip
            ILogger<OrderService> logger)
        {
            _context = ordersContext ?? throw new ArgumentNullException(nameof(ordersContext));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _catalogContext = catalogContext ?? throw new ArgumentNullException(nameof(catalogContext)); // Dodaj null check
            _dbNotificationService = dbNotificationService ?? throw new ArgumentNullException(nameof(dbNotificationService));
            _fcmPushNotificationService = fcmPushNotificationService ?? throw new ArgumentNullException(nameof(fcmPushNotificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<OrderModel> CreateOrderAsync(int buyerId, int storeId) // ➤ Pazi na tip buyerId (int vs string)!
        {
            // --- Validation ---
            if (storeId <= 0) throw new ArgumentException("Invalid StoreId.", nameof(storeId));
            // ➤ Treba provjeriti da li buyerId postoji i da li je buyer, i da li storeId postoji!
            // ➤ Treba konvertovati buyerId u string ako je User.Id string
            string buyerUserIdString = buyerId.ToString(); // Primjer konverzije ako je User.Id string
            var buyerUser = await _userManager.FindByIdAsync(buyerUserIdString);
            if (buyerUser == null) throw new KeyNotFoundException($"Buyer with ID {buyerId} not found.");
            // TODO: Provjeri da li store postoji (treba StoreDbContext ili StoreService)

            var order = new OrderModel
            {
                BuyerId = buyerId, // Čuvaj kao int ako je tako u modelu
                StoreId = storeId,
                Status = OrderStatus.Requested,
                Time = DateTime.UtcNow,
            };

            _context.Orders.Add(order);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully created Order {OrderId} for Buyer {BuyerId}, Store {StoreId}", order.Id, buyerId, storeId);

                // --- SLANJE NOTIFIKACIJA ---
                var sellerUser = await _userManager.Users.FirstOrDefaultAsync(u => u.StoreId == storeId);
                if (sellerUser != null)
                {
                    // Pokušaj poslati notifikacije, ali ne prekidaj ako ne uspiju
                    try
                    {
                        string message = $"Primili ste novu narudžbu #{order.Id}.";
                        string link = $"/seller/orders/{order.Id}"; // Primjer

                        // 1. Upis u našu bazu
                        await _dbNotificationService.CreateNotificationAsync(sellerUser.Id, message, "Order", order.Id, link);

                        // 2. Slanje FCM Push Notifikacije
                        if (!string.IsNullOrWhiteSpace(sellerUser.FcmDeviceToken))
                        {
                            await _fcmPushNotificationService.SendPushNotificationAsync(
                                sellerUser.FcmDeviceToken,
                                "Nova Narudžba",
                                $"Stigla je narudžba #{order.Id} za vašu prodavnicu.",
                                new Dictionary<string, string> { { "orderId", order.Id.ToString() }, { "screen", "OrderDetail" } }
                            );
                        }
                        else
                        {
                            _logger.LogWarning("Seller {SellerId} does not have an FCM token registered for new Order {OrderId}.", sellerUser.Id, order.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send notifications for new Order {OrderId} for Seller {SellerId}", order.Id, sellerUser.Id);
                    }
                }
                else
                {
                    _logger.LogWarning("Could not find seller for StoreId {StoreId} to send new order notification for Order {OrderId}.", storeId, order.Id);
                }
                // --------------------------

                return order;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating order for Buyer {BuyerId}, Store {StoreId}", buyerId, storeId);
                throw; // Ponovo baci grešku da kontroler zna
            }
        }

        public async Task<OrderModel?> GetOrderByIdAsync(int orderId) // Vraća model, kontroler mapira ako treba
        {
            // ... (postojeći kod sa AsNoTracking) ...
            _logger.LogDebug("Fetching Order with Id {OrderId}", orderId);
            return await _context.Orders
                                 .Include(o => o.OrderItems)
                                 .AsNoTracking()
                                 .FirstOrDefaultAsync(o => o.Id == orderId);
        }

        public async Task<IEnumerable<OrderModel>> GetAllOrdersAsync() // Vraća modele
        {
            // ... (postojeći kod sa AsNoTracking) ...
            _logger.LogDebug("Fetching all orders");
            return await _context.Orders
                                 .Include(o => o.OrderItems)
                                 .AsNoTracking()
                                 .ToListAsync();
        }

        public async Task<IEnumerable<OrderModel>> GetOrdersByBuyerAsync(int buyerId) // Vraća modele
        {
            // ... (postojeći kod sa AsNoTracking) ...
            // ➤ Pazi na tip buyerId (int vs string)!
            _logger.LogDebug("Fetching orders for BuyerId {BuyerId}", buyerId);
            return await _context.Orders
                                 .Include(o => o.OrderItems)
                                 .Where(o => o.BuyerId == buyerId) // Radi samo ako je BuyerId int
                                 .AsNoTracking()
                                 .ToListAsync();
        }

        public async Task<IEnumerable<OrderModel>> GetOrdersByStoreAsync(int storeId) // Vraća modele
        {
            // ... (postojeći kod sa AsNoTracking) ...
            _logger.LogDebug("Fetching orders for StoreId {StoreId}", storeId);
            return await _context.Orders
                               .Include(o => o.OrderItems)
                               .Where(o => o.StoreId == storeId)
                               .AsNoTracking()
                               .ToListAsync();
        }

        // Nova metoda za dohvat detalja sa mapiranjem u DTO
        public async Task<OrderDetailDto?> GetOrderDetailsForSellerAsync(string sellerUserId, int orderId)
        {
            // ... (Implementacija KAO U MOM PRETHODNOM ODGOVORU, sa dohvaćanjem Producta iz _catalogContext) ...
            if (orderId <= 0) throw new ArgumentException("Invalid Order ID.", nameof(orderId));
            if (string.IsNullOrWhiteSpace(sellerUserId)) throw new ArgumentNullException(nameof(sellerUserId));

            var sellerUser = await _userManager.FindByIdAsync(sellerUserId);
            if (sellerUser == null || !sellerUser.StoreId.HasValue) throw new UnauthorizedAccessException("Seller is not authorized or does not own a store.");
            int sellerStoreId = sellerUser.StoreId.Value;

            var order = await _context.Orders
                .Where(o => o.Id == orderId)
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync();

            if (order == null) return null;
            if (order.StoreId != sellerStoreId) throw new UnauthorizedAccessException("Seller is not authorized to access this order.");

            // Dohvati dodatne podatke
            OrderUserInfoDto? buyerInfoDto = null;
            var buyer = await _userManager.FindByIdAsync(order.BuyerId.ToString()); // ➤ Pazi na tip BuyerId
            if (buyer != null) buyerInfoDto = new OrderUserInfoDto { Id = buyer.Id, UserName = buyer.UserName, Email = buyer.Email };

            var productIds = order.OrderItems.Select(oi => oi.ProductId).Distinct().ToList();
            var products = await _catalogContext.Products // ➤ Koristi _catalogContext
                                   .Where(p => productIds.Contains(p.Id))
                                   .Include(p => p.Pictures)
                                   .ToDictionaryAsync(p => p.Id);

            // Mapiraj u DTO
            var orderDetailDto = new OrderDetailDto
            {
                Id = order.Id,
                OrderDate = order.Time,
                Status = order.Status.ToString(),
                TotalAmount = order.Total ?? 0m,
                StoreId = order.StoreId,
                BuyerInfo = buyerInfoDto,
                ShippingAddress = null, // TODO
                Items = order.OrderItems.Select(oi =>
                {
                    products.TryGetValue(oi.ProductId, out var product);
                    return new OrderItemDto
                    {
                        Id = oi.Id,
                        ProductId = oi.ProductId,
                        ProductName = product?.Name ?? "N/A",
                        Quantity = oi.Quantity,
                        PricePerProduct = oi.Price,
                        Subtotal = oi.Quantity * oi.Price,
                        ProductImageUrl = product?.Pictures?.FirstOrDefault()?.Url
                    };
                }).ToList()
            };
            return orderDetailDto;
        }

        // Koristimo ovu metodu za update statusa koja radi autorizaciju i šalje notifikacije
        public async Task<bool> UpdateOrderStatusForSellerAsync(string sellerUserId, UpdateOrderStatusRequestDto updateDto)
        {
            if (updateDto == null) throw new ArgumentNullException(nameof(updateDto));
            if (string.IsNullOrWhiteSpace(sellerUserId)) throw new ArgumentNullException(nameof(sellerUserId));
            if (updateDto.OrderId <= 0) throw new ArgumentException("Invalid Order ID.", nameof(updateDto.OrderId));
            if (string.IsNullOrWhiteSpace(updateDto.NewStatus)) throw new ArgumentException("New status is required.", nameof(updateDto.NewStatus));

            _logger.LogInformation("Attempting to update status for Order {OrderId} to {NewStatus} by User {UserId}",
                updateDto.OrderId, updateDto.NewStatus, sellerUserId);

            var sellerUser = await _userManager.FindByIdAsync(sellerUserId);
            if (sellerUser == null || !sellerUser.StoreId.HasValue)
            {
                _logger.LogWarning("UpdateOrderStatus: Seller {UserId} not found or does not have an associated store.", sellerUserId);
                throw new UnauthorizedAccessException("Seller is not authorized or does not own a store.");
            }
            int sellerStoreId = sellerUser.StoreId.Value;

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == updateDto.OrderId);
            if (order == null)
            {
                _logger.LogWarning("UpdateOrderStatus: Order {OrderId} not found.", updateDto.OrderId);
                return false; // NotFound
            }

            if (order.StoreId != sellerStoreId)
            {
                _logger.LogWarning("Forbidden status update attempt by User {UserId} for Order {OrderId}.", sellerUserId, updateDto.OrderId);
                throw new UnauthorizedAccessException("Seller is not authorized to update status for this order.");
            }

            if (!Enum.TryParse<OrderStatus>(updateDto.NewStatus, true, out var newStatusEnum))
            {
                _logger.LogWarning("UpdateOrderStatus: Invalid NewStatus value '{NewStatus}'.", updateDto.NewStatus);
                throw new ArgumentException($"Invalid status value: {updateDto.NewStatus}.");
            }

            if (!IsValidStatusTransition(order.Status, newStatusEnum))
            {
                _logger.LogWarning("UpdateOrderStatus: Invalid status transition from {CurrentStatus} to {NewStatus} for Order {OrderId}.", order.Status, newStatusEnum, updateDto.OrderId);
                throw new InvalidOperationException($"Cannot change order status from {order.Status} to {newStatusEnum}.");
            }

            order.Status = newStatusEnum;
            _context.Entry(order).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully updated status for Order {OrderId} to {NewStatus} by User {UserId}",
                                       updateDto.OrderId, newStatusEnum, sellerUserId);

                // --- SLANJE NOTIFIKACIJA ---
                var buyerUser = await _userManager.FindByIdAsync(order.BuyerId.ToString()); // ➤ Pazi na tip BuyerId
                if (buyerUser != null)
                {
                    try
                    {
                        string message = $"Status vaše narudžbe #{order.Id} je promijenjen u '{newStatusEnum}'.";
                        string link = $"/buyer/orders/{order.Id}";

                        // 1. Upis u našu bazu
                        await _dbNotificationService.CreateNotificationAsync(buyerUser.Id, message, "Order", order.Id, link);

                        // 2. Slanje FCM Push
                        if (!string.IsNullOrWhiteSpace(buyerUser.FcmDeviceToken))
                        {
                            await _fcmPushNotificationService.SendPushNotificationAsync(
                                buyerUser.FcmDeviceToken,
                                "Status Narudžbe Ažuriran",
                                message,
                                new Dictionary<string, string> { { "orderId", order.Id.ToString() }, { "newStatus", newStatusEnum.ToString() } }
                            );
                        }
                        else { _logger.LogWarning("Buyer {BuyerId} missing FCM token for Order {OrderId}.", buyerUser.Id, order.Id); }

                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send notifications for Order {OrderId} status update for Buyer {BuyerId}", order.Id, buyerUser.Id);
                    }
                }
                else { _logger.LogWarning("Could not find buyer {BuyerId} for Order {OrderId} notification.", order.BuyerId, order.Id); }
                // --- KRAJ NOTIFIKACIJA ---

                return true; // Uspjeh
            }
            catch (DbUpdateConcurrencyException ex) { _logger.LogWarning(ex, "Concurrency update status OrderId {OrderId}", updateDto.OrderId); return false; }
            catch (DbUpdateException ex) { _logger.LogError(ex, "Error updating status OrderId {OrderId}", updateDto.OrderId); return false; }
        }

        // Privatna helper metoda za validaciju tranzicije statusa
        private bool IsValidStatusTransition(OrderStatus currentStatus, OrderStatus newStatus)
        {
            // ... (Implementiraj pravila) ...
            switch (currentStatus)
            {
                case OrderStatus.Requested: return newStatus == OrderStatus.Confirmed || newStatus == OrderStatus.Rejected || newStatus == OrderStatus.Cancelled;
                case OrderStatus.Confirmed: return newStatus == OrderStatus.Ready || newStatus == OrderStatus.Sent || newStatus == OrderStatus.Cancelled;
                case OrderStatus.Ready: return newStatus == OrderStatus.Delivered || newStatus == OrderStatus.Cancelled;
                case OrderStatus.Sent: return newStatus == OrderStatus.Delivered || newStatus == OrderStatus.Cancelled;
                case OrderStatus.Delivered: case OrderStatus.Rejected: case OrderStatus.Cancelled: return false;
                default: return false;
            }
        }

        public async Task<bool> DeleteOrderAsync(int orderId)
        {
            // ... (postojeći kod za brisanje, možda dodati notifikaciju?) ...
            var order = await _context.Orders.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return false;
            _context.Orders.Remove(order);
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Deleted Order with Id {OrderId}", orderId);
                // TODO: Poslati notifikaciju Buyer-u? Selleru?
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
            _logger.LogInformation("Dohvatanje narudžbi DTO za sellerUserId {SellerUserId}", sellerUserId);
            var user = await _userManager.FindByIdAsync(sellerUserId);
            if (user == null || !user.StoreId.HasValue)
            {
                _logger.LogWarning("Korisnik nije pronađen ili nema StoreId: {SellerUserId}", sellerUserId);
                return Enumerable.Empty<OrderSummaryDto>();
            }
            int storeId = user.StoreId.Value;
            var orders = await _context.Orders
                .Where(o => o.StoreId == storeId)
                .OrderByDescending(o => o.Time)
                .Select(o => new OrderSummaryDto
                {
                    OrderId = o.Id,
                    OrderDate = o.Time,
                    TotalAmount = o.Total ?? 0m,
                    Status = o.Status,
                    StoreId = o.StoreId,
                    ItemCount = o.OrderItems.Count() // Izbroj iteme
                                                     // BuyerName = ... // Treba join sa UsersDbContext/UserManager
                })
                .AsNoTracking()
                .ToListAsync();
            _logger.LogInformation("Vraćeno {Count} DTO narudžbi za StoreId {StoreId}", orders.Count, storeId);
            return orders;
        }

        public Task<bool> UpdateOrderStatusAsync(int orderId, OrderStatus newStatus)
        {
            throw new NotImplementedException();
        }
    }
}