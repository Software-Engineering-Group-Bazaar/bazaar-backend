using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notifications.Interfaces;
using Order.Interface;
using Order.Models;
using Order.Models.DTOs.Buyer;
using Users.Models;

namespace Order.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class OrderBuyerController : ControllerBase
    {
        private readonly ILogger<OrderBuyerController> _logger;
        private readonly IOrderService _orderService;
        private readonly IOrderItemService _orderItemService;
        private readonly UserManager<User> _userManager;

        private readonly INotificationService _notificationService;

        private readonly IPushNotificationService _pushNotificationService;

        public OrderBuyerController(
            ILogger<OrderBuyerController> logger,
            IOrderService orderService,
            IOrderItemService orderItemService,
            UserManager<User> userManager,
            INotificationService notificationService,
            IPushNotificationService pushNotificationService)

        {
            _logger = logger;
            _orderService = orderService;
            _orderItemService = orderItemService;
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _pushNotificationService = pushNotificationService ?? throw new ArgumentNullException(nameof(pushNotificationService));
        }

        // GET /api/OrderBuyer/order
        [HttpGet("order")]
        [ProducesResponseType(typeof(IEnumerable<OrderGetBuyerDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<OrderGetBuyerDto>>> GetAllOrders()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("[StoresController] CreateStore - Could not find user ID claim for the authenticated user.");
                return Unauthorized("User ID claim not found."); // 401 Unauthorized
            }

            _logger.LogInformation("Attempting to retrieve all orders of user.");
            try
            {
                var orders = await _orderService.GetOrdersByBuyerAsync(userId);

                var orderDtos = orders.Select(o => new OrderGetBuyerDto
                {
                    Id = o.Id,
                    BuyerId = o.BuyerId,
                    StoreId = o.StoreId,
                    Status = o.Status.ToString(),
                    Time = o.Time,
                    Total = o.Total,
                    OrderItems = o.OrderItems.Select(oi => new OrderItemGetBuyerDto
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

        // GET /api/OrderBuyer/order/{id}
        [HttpGet("order/{id}")]
        [ProducesResponseType(typeof(OrderGetBuyerDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<OrderGetBuyerDto>> GetOrderById(int id)
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

                var orderDto = new OrderGetBuyerDto
                {
                    Id = order.Id,
                    BuyerId = order.BuyerId,
                    StoreId = order.StoreId,
                    Status = order.Status.ToString(),
                    Time = order.Time,
                    Total = order.Total,
                    OrderItems = order.OrderItems.Select(oi => new OrderItemGetBuyerDto
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

        // POST /api/OrderBuyer/order/create
        [HttpPost("order/create")]
        [ProducesResponseType(typeof(OrderGetBuyerDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<OrderGetBuyerDto>> CreateOrder([FromBody] OrderCreateBuyerDto createDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("[StoresController] CreateStore - Could not find user ID claim for the authenticated user.");
                return Unauthorized("User ID claim not found."); // 401 Unauthorized
            }

            _logger.LogInformation("Attempting to create a new order for BuyerId: {BuyerId}, StoreId: {StoreId}", userId, createDto.StoreId);

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Create order request failed model validation. Errors: {@ModelState}", ModelState.Values.SelectMany(v => v.Errors));
                return BadRequest(ModelState);
            }

            // Optional: Add validation to check if BuyerId and StoreId actually exist
            // var buyerExists = await _userManager.FindByIdAsync(userId) != null;
            // var storeExists = _storeService.GetStoreById(createDto.StoreId) != null; // Assuming synchronous version exists or make async
            // if (!buyerExists) return BadRequest($"Buyer with ID {userId} not found.");
            // if (!storeExists) return BadRequest($"Store with ID {createDto.StoreId} not found.");

            try
            {
                var createdOrder = await _orderService.CreateOrderAsync(userId, createDto.StoreId);
                var listitems = new List<OrderItemGetBuyerDto>();
                foreach (var item in createDto.OrderItems)
                {
                    var x = await _orderItemService.CreateOrderItemAsync(createdOrder.Id, item.ProductId, item.Quantity);
                    listitems.Add(new OrderItemGetBuyerDto
                    {
                        Id = x.Id,
                        ProductId = x.ProductId,
                        Price = x.Price,
                        Quantity = x.Quantity,
                    });
                }
                // Map the created order (which won't have items yet) to the DTO
                var orderDto = new OrderGetBuyerDto
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
                _logger.LogWarning(ex, "Failed to create order due to invalid arguments (BuyerId: {BuyerId}, StoreId: {StoreId})", userId, createDto.StoreId);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating an order for BuyerId: {BuyerId}, StoreId: {StoreId}", userId, createDto.StoreId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred while creating the order.");
            }
        }
    }
}