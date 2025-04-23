using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using AdminApi.DTOs;
using Catalog.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notifications.Interfaces;
using Order.DTOs;
using Order.Interface;
using Order.Models;
using Users.Models;

namespace Order.Controllers
{
    [Route("api/[controller]")]

    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;

        private readonly IOrderItemService _orderItemService;

        private readonly ILogger<OrderController> _logger;

        private readonly UserManager<User> _userManager;

        private readonly INotificationService _notificationService;

        private readonly IPushNotificationService _pushNotificationService;

        public OrderController(
            ILogger<OrderController> logger,
            IOrderService orderService,
            IOrderItemService orderItemService,
            UserManager<User> userManager,
            INotificationService notificationService,
            IPushNotificationService pushNotificationService
            )

        {
            _logger = logger;
            _orderService = orderService;
            _orderItemService = orderItemService;
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _pushNotificationService = pushNotificationService ?? throw new ArgumentNullException(nameof(pushNotificationService));
        }

        // GET /api//order
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<OrderGetDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<OrderGetDto>>> GetAllOrders()
        {
            _logger.LogInformation("Attempting to retrieve all orders.");
            try
            {
                var orders = await _orderService.GetAllOrdersAsync();

                var orderDtos = orders.Select(o => new OrderGetDto
                {
                    Id = o.Id,
                    BuyerId = o.BuyerId,
                    StoreId = o.StoreId,
                    Status = o.Status.ToString(),
                    Time = o.Time,
                    Total = o.Total,
                    OrderItems = o.OrderItems.Select(oi => new OrderItemGetDto
                    {
                        Id = oi.Id,
                        ProductId = oi.ProductId,
                        Price = oi.Price,
                        Quantity = oi.Quantity
                    }).ToList()
                }).ToList();

                _logger.LogInformation("Successfully retrieved {OrderCount} orders.", orderDtos.Count);
                return Ok(orderDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving all orders.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred while retrieving orders.");
            }
        }

        // GET /api/order/{id}
        [HttpGet("{id}")]
        [Authorize(Roles = "Admin, Seller")]
        [ProducesResponseType(typeof(OrderGetDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<OrderGetDto>> GetOrderById(int id)
        {
            _logger.LogInformation("Attempting to retrieve order with ID: {OrderId}", id);
            if (id <= 0)
            {
                _logger.LogWarning("GetOrderById request failed validation: Invalid ID {OrderId}", id);
                return BadRequest("Invalid Order ID provided.");
            }

            try
            {
                var order = await _orderService.GetOrderByIdAsync(id);

                if (order == null)
                {
                    _logger.LogWarning("Order with ID {OrderId} not found.", id);
                    return NotFound($"Order with ID {id} not found.");
                }

                var orderDto = new OrderGetDto
                {
                    Id = order.Id,
                    BuyerId = order.BuyerId,
                    StoreId = order.StoreId,
                    Status = order.Status.ToString(),
                    Time = order.Time,
                    Total = order.Total,
                    OrderItems = order.OrderItems.Select(oi => new OrderItemGetDto
                    {
                        Id = oi.Id,
                        ProductId = oi.ProductId,
                        Price = oi.Price,
                        Quantity = oi.Quantity
                    }).ToList()
                };

                _logger.LogInformation("Successfully retrieved order with ID: {OrderId}", id);
                return Ok(orderDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving order with ID: {OrderId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred while retrieving the order.");
            }
        }

        // POST /api/order/create
        [Authorize(Roles = "Admin, Buyer")]
        [HttpPost("create")]
        [ProducesResponseType(typeof(OrderGetDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<OrderGetDto>> CreateOrder([FromBody] OrderCreateDto createDto)
        {
            _logger.LogInformation("Attempting to create a new order for BuyerId: {BuyerId}, StoreId: {StoreId}", createDto.BuyerId, createDto.StoreId);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Create order request failed model validation. Errors: {@ModelState}", ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }

            // Optional: Add validation to check if BuyerId and StoreId actually exist
            // var buyerExists = await _userManager.FindByIdAsync(createDto.BuyerId) != null;
            // var storeExists = _storeService.GetStoreById(createDto.StoreId) != null; // Assuming synchronous version exists or make async
            // if (!buyerExists) return BadRequest($"Buyer with ID {createDto.BuyerId} not found.");
            // if (!storeExists) return BadRequest($"Store with ID {createDto.StoreId} not found.");

            try
            {
                var createdOrder = await _orderService.CreateOrderAsync(createDto.BuyerId, createDto.StoreId);
                var listitems = new List<OrderItemGetDto>();
                foreach (var item in createDto.OrderItems)
                {
                    var x = await _orderItemService.CreateOrderItemAsync(createdOrder.Id, item.ProductId, item.Quantity);
                    listitems.Add(new OrderItemGetDto
                    {
                        Id = x.Id,
                        ProductId = x.ProductId,
                        Price = x.Price,
                        Quantity = x.Quantity,
                    });
                }
                // Map the created order (which won't have items yet) to the DTO
                var orderDto = new OrderGetDto
                {
                    Id = createdOrder.Id,
                    BuyerId = createdOrder.BuyerId,
                    StoreId = createdOrder.StoreId,
                    Status = createdOrder.Status.ToString(),
                    Time = createdOrder.Time,
                    Total = createdOrder.Total, // Will likely be null or 0 initially
                    OrderItems = listitems // Empty list
                };

                var sellerUser = await _userManager.Users.FirstOrDefaultAsync(u => u.StoreId == createdOrder.StoreId);

                if (sellerUser != null)
                {
                    string notificationMessage = $"Nova narudžba #{createdOrder.Id} je kreirana za vašu prodavnicu.";

                    await _notificationService.CreateNotificationAsync(
                            sellerUser.Id,
                            notificationMessage,
                            createdOrder.Id
                        );
                    _logger.LogInformation("Notification creation task initiated for Seller {SellerUserId} for new Order {OrderId}.", sellerUser.Id, createdOrder.Id);

                    if (!string.IsNullOrWhiteSpace(sellerUser.FcmDeviceToken))
                    {
                        try
                        {
                            string pushTitle = "Nova Narudžba!";
                            string pushBody = $"Dobili ste narudžbu #{createdOrder.Id}.";
                            var pushData = new Dictionary<string, string> {
                            { "orderId", createdOrder.Id.ToString() },
                            { "screen", "OrderDetail" } // Primjer za frontend navigaciju
                    };

                            await _pushNotificationService.SendPushNotificationAsync(
                                sellerUser.FcmDeviceToken,
                                pushTitle,
                                pushBody,
                                pushData
                            );
                            _logger.LogInformation("Push Notification task initiated for Seller {SellerUserId} for new Order {OrderId}.", sellerUser.Id, createdOrder.Id);
                        }
                        catch (Exception pushEx)
                        {
                            _logger.LogError(pushEx, "Failed to send Push Notification to Seller {SellerUserId} for Order {OrderId}.", sellerUser.Id, createdOrder.Id);
                        }
                    }

                }
                else
                {
                    _logger.LogWarning("Could not find seller user for StoreId {StoreId} to send new order notification for Order {OrderId}.", createdOrder.StoreId, createdOrder.Id);
                }

                _logger.LogInformation("Successfully created order with ID: {OrderId}", createdOrder.Id);
                // Return 201 Created with the location of the newly created resource and the resource itself
                return CreatedAtAction(nameof(GetOrderById), new { id = createdOrder.Id }, orderDto);
            }
            catch (ArgumentException ex) // Catch specific exceptions from the service if possible
            {
                _logger.LogWarning(ex, "Failed to create order due to invalid arguments (BuyerId: {BuyerId}, StoreId: {StoreId})", createDto.BuyerId, createDto.StoreId);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating an order for BuyerId: {BuyerId}, StoreId: {StoreId}", createDto.BuyerId, createDto.StoreId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred while creating the order.");
            }
        }

        // PUT /api/order/update/{id} --> update the whole thing
        [Authorize(Roles = "Admin, Seller")]
        [HttpPut("update/{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateOrder(int id, [FromBody] OrderUpdateDto updateDto)
        {
            // ako znm da ce neki item fail uraditi onda necu ni glavni update raditi
            // also ako trb dodati item dodaj ga
            if (updateDto.OrderItems is not null)
            {
                try
                {
                    _logger.LogInformation("Attempting to check order validity for order items");
                    foreach (var item in updateDto.OrderItems)
                    {
                        var f = await _orderItemService.CheckValid(item.Id, item.Quantity, item.ProductId, item.Price);
                        if (!f)
                        {
                            var orderitem = await _orderItemService.CreateOrderItemAsync(id, item.ProductId, item.Quantity);
                            item.Id = orderitem.Id;
                            //_logger.LogInformation($"")
                        }
                    }
                }
                catch (ArgumentException a)
                {
                    return BadRequest($"OrderItem data invalid. {a.Message}");
                }
            }


            _logger.LogInformation("Attempting to update order for order ID: {OrderId}", id);
            OrderStatus status = OrderStatus.Requested;
            if (updateDto.Status is not null)
            {
                if (Enum.TryParse(updateDto.Status, true, out OrderStatus result))
                {
                    status = result;
                }
                else
                {
                    return BadRequest($"Nevalidan status {updateDto.Status}");
                }
            }

            var success = await _orderService.UpdateOrderAsync(id, updateDto.BuyerId, updateDto.StoreId, status, updateDto.Time, updateDto.Total);
            if (!success)
            {
                return BadRequest("Order update failed.");
            }
            if (updateDto.OrderItems is not null)
            {
                var tasks = updateDto.OrderItems.Select(item =>
                    _orderItemService.ForceUpdateOrderItemAsync(item.Id, item.Quantity, item.ProductId, item.Price)
                );
                var results = await Task.WhenAll(tasks);
                var fail = results.Any(flag => !flag);
                if (fail)
                {
                    return BadRequest("OrderItem update failed.");
                }
            }
            return NoContent();
        }

        // PUT /api/order/update/status/{id} --> Primarily for updating Status
        [HttpPut("update/status/{id}")]
        [Authorize(Roles = "Admin, Seller")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] OrderUpdateStatusDto updateDto)
        {
            _logger.LogInformation("Attempting to update status for order ID: {OrderId} to {NewStatus}", id, updateDto.NewStatus);

            if (id <= 0)
            {
                _logger.LogWarning("UpdateOrderStatus request failed validation: Invalid ID {OrderId}", id);
                return BadRequest("Invalid Order ID provided.");
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Update order status request failed model validation for Order ID {OrderId}. Errors: {@ModelState}", id, ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }

            try
            {
                OrderStatus status = OrderStatus.Requested;
                if (updateDto.NewStatus is not null)
                {
                    if (Enum.TryParse(updateDto.NewStatus, true, out OrderStatus result))
                    {
                        status = result;
                    }
                    else
                    {
                        return BadRequest($"Nevalidan status {updateDto.NewStatus}");
                    }
                }

                var success = await _orderService.UpdateOrderStatusAsync(id, status);

                if (!success)
                {
                    _logger.LogWarning("Failed to update status for order ID: {OrderId}. Order might not exist or a concurrency issue occurred.", id);
                    var orderExists = await _orderService.GetOrderByIdAsync(id) != null;
                    if (!orderExists)
                    {
                        return NotFound($"Order with ID {id} not found.");
                    }

                    return BadRequest($"Failed to update status for order ID: {id}. See logs for details.");
                }
                try
                {
                    var updatedOrder = await _orderService.GetOrderByIdAsync(id);

                    var buyerUser = await _userManager.FindByIdAsync(updatedOrder.BuyerId);

                    if (updatedOrder != null && !string.IsNullOrWhiteSpace(updatedOrder.BuyerId))
                    {
                        string notificationMessage = $"Status Vaše narudžbe #{id} je ažuriran na '{status}'.";

                        await _notificationService.CreateNotificationAsync(
                            updatedOrder.BuyerId,
                            notificationMessage,
                            id
                        );
                        _logger.LogInformation("Notification creation task initiated for Buyer {BuyerId} for Order {OrderId} status update.", updatedOrder.BuyerId, id);

                        if (!string.IsNullOrWhiteSpace(buyerUser.FcmDeviceToken))
                        {
                            try
                            {
                                string pushTitle = "Status Narudžbe Ažuriran";
                                string pushBody = $"Status narudžbe #{id} je sada: {status}.";
                                // Opcionalno: Dodaj podatke za navigaciju u aplikaciji
                                var pushData = new Dictionary<string, string> {
                                         { "orderId", id.ToString() },
                                         { "screen", "OrderDetail" } // Primjer
                                     };

                                // Pozovi servis za slanje PUSH notifikacije
                                await _pushNotificationService.SendPushNotificationAsync(
                                    buyerUser.FcmDeviceToken,
                                    pushTitle,
                                    pushBody,
                                    pushData
                                );
                                _logger.LogInformation("Push Notification task initiated for Buyer {BuyerId} for Order {OrderId} status update.", buyerUser.Id, id);


                            }
                            catch (Exception pushEx)
                            {
                                // Loguj grešku slanja push notifikacije ali ne prekidaj izvršavanje
                                _logger.LogError(pushEx, "Failed to send Push Notification to Buyer {BuyerId} for Order {OrderId}.", buyerUser.Id, id);
                            }
                        }
                        else if (updatedOrder != null)
                        {
                            _logger.LogWarning("Order {OrderId} was updated, but BuyerId was missing. Cannot send notification.", id);
                        }
                        else
                        {
                            _logger.LogError("Order {OrderId} was updated successfully, but could not be retrieved afterwards to send notification.", id);
                        }
                    }
                }
                catch (Exception notificationEx)
                {
                    _logger.LogError(notificationEx, "Failed to create notification for Buyer after successfully updating status for Order {OrderId}.", id);
                }
                _logger.LogInformation("Successfully updated status for order ID: {OrderId} to {NewStatus}", id, updateDto.NewStatus);
                return NoContent(); // Standard success response for PUT when no content is returned
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating status for order ID: {OrderId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred while updating the order status.");
            }
        }

        // DELETE /api/order/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin, Seller")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            _logger.LogInformation("Attempting to delete order with ID: {OrderId}", id);

            if (id <= 0)
            {
                _logger.LogWarning("DeleteOrder request failed validation: Invalid ID {OrderId}", id);
                return BadRequest("Invalid Order ID provided.");
            }

            try
            {
                var success = await _orderService.DeleteOrderAsync(id);

                if (!success)
                {
                    // Service logs if not found
                    _logger.LogWarning("Failed to delete order with ID: {OrderId}. Order might not exist.", id);
                    return NotFound($"Order with ID {id} not found."); // Return 404 if the delete operation indicated not found
                }

                _logger.LogInformation("Successfully deleted order with ID: {OrderId}", id);
                return NoContent(); // Standard success response for DELETE
            }
            catch (DbUpdateException dbEx) // Catch potential FK constraint issues if cascade delete isn't set up perfectly
            {
                _logger.LogError(dbEx, "Database error occurred while deleting order ID: {OrderId}. It might be referenced elsewhere.", id);
                // Consider returning 409 Conflict if it's due to dependencies
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while deleting order {id}. It might have related data preventing deletion.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while deleting order ID: {OrderId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred while deleting the order.");
            }
        }

        // GET /api/order/my-store-orders
        [HttpGet("MyStore")]
        [Authorize(Roles = "Seller,Admin")]
        [ProducesResponseType(typeof(IEnumerable<OrderGetDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<OrderGetDto>>> GetMyStoreOrders()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("GetMyStoreOrders failed: User ID claim not found.");
                return Unauthorized("User ID claim not found.");
            }

            _logger.LogInformation("Seller {UserId} attempting to retrieve orders for their store.", userId);

            try
            {
                var sellerUser = await _userManager.FindByIdAsync(userId);
                if (sellerUser == null)
                {
                    _logger.LogError("GetMyStoreOrders failed: Authenticated User {UserId} not found in database.", userId);
                    return StatusCode(StatusCodes.Status500InternalServerError, "Authenticated user could not be found.");
                }

                if (sellerUser.StoreId == null)
                {
                    _logger.LogWarning("Seller {UserId} attempted to get store orders, but has no StoreId assigned.", userId);
                    return NotFound("No store is associated with your account.");
                }

                int storeId = sellerUser.StoreId.Value;
                _logger.LogInformation("Seller {UserId} fetching orders for their StoreId {StoreId}", userId, storeId);

                var orders = await _orderService.GetOrdersByStoreAsync(storeId);

                var orderDtos = orders.Select(o => new OrderGetDto
                {
                    Id = o.Id,
                    BuyerId = o.BuyerId,
                    StoreId = o.StoreId,
                    Status = o.Status.ToString(),
                    Time = o.Time,
                    Total = o.Total,
                    OrderItems = o.OrderItems?.Select(oi => new OrderItemGetDto
                    {
                        Id = oi.Id,
                        ProductId = oi.ProductId,
                        Price = oi.Price,
                        Quantity = oi.Quantity
                    }).ToList() ?? new List<OrderItemGetDto>()
                }).ToList();

                _logger.LogInformation("Successfully retrieved {OrderCount} orders for Seller {UserId}'s Store ID {StoreId}", orderDtos.Count, userId, storeId);
                return Ok(orderDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while retrieving orders for Seller {UserId}'s store.", userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred while retrieving store orders.");
            }
        }

        // GET /api/order/store/{storeId}
        [HttpGet("store/{storeId:int}")]
        [Authorize(Roles = "Admin, Seller")]
        [ProducesResponseType(typeof(IEnumerable<OrderGetDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<OrderGetDto>>> GetOrdersForStore(int storeId)
        {
            _logger.LogInformation("Attempting to retrieve orders for Store ID: {StoreId}", storeId);

            if (storeId <= 0)
            {
                _logger.LogWarning("GetOrdersForStore request failed validation: Invalid Store ID {StoreId}", storeId);
                return BadRequest("Invalid Store ID provided.");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            try
            {
                var requestingUser = await _userManager.FindByIdAsync(userId);
                if (requestingUser == null) return NotFound("Requesting user not found.");

                bool isAdmin = User.IsInRole("Admin");
                bool isSeller = User.IsInRole("Seller");

                if (isSeller && !isAdmin)
                {
                    if (requestingUser.StoreId != storeId)
                    {
                        _logger.LogWarning("Forbidden: Seller {UserId} attempted to access orders for store {StoreId}, but owns store {OwnedStoreId}",
                                           userId, storeId, requestingUser.StoreId?.ToString() ?? "None");
                        return Forbid("You are not authorized to view orders for this store."); // 403 Forbidden
                    }
                    _logger.LogInformation("Seller {UserId} authorized to view orders for their store {StoreId}", userId, storeId);
                }
                else if (!isAdmin)
                {
                    _logger.LogWarning("Forbidden: User {UserId} with roles {Roles} attempted to access orders for store {StoreId}",
                                         userId, string.Join(",", User.FindAll(ClaimTypes.Role).Select(c => c.Value)), storeId);
                    return Forbid();
                }
                // Admin može nastaviti
                if (isAdmin) _logger.LogInformation("Admin {UserId} authorized to view orders for store {StoreId}", userId, storeId);


                var orders = await _orderService.GetOrdersByStoreAsync(storeId);

                var orderDtos = orders.Select(o => new OrderGetDto
                {
                    Id = o.Id,
                    BuyerId = o.BuyerId,
                    StoreId = o.StoreId,
                    Status = o.Status.ToString(),
                    Time = o.Time,
                    Total = o.Total,
                    OrderItems = o.OrderItems?.Select(oi => new OrderItemGetDto
                    {
                        Id = oi.Id,
                        ProductId = oi.ProductId,
                        Price = oi.Price,
                        Quantity = oi.Quantity
                    }).ToList() ?? new List<OrderItemGetDto>()
                }).ToList();


                _logger.LogInformation("Successfully retrieved {OrderCount} orders for Store ID: {StoreId}", orderDtos.Count, storeId);
                return Ok(orderDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving orders for Store ID: {StoreId}", storeId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred while retrieving orders.");
            }
        }
    }
}
