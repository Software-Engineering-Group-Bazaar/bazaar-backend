using System.Security.Claims;
using Catalog.Services;
using Inventory.Dtos;
using Inventory.Interfaces;
using Inventory.Models;
using Loyalty.Interfaces;
using Loyalty.Models;
using MarketingAnalytics.Interfaces;
using MarketingAnalytics.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notifications.Interfaces;
using Notifications.Services;
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

        private readonly IInventoryService _inventoryService;
        private readonly InventoryDbContext _inventoryContext;
        private readonly IAdService _adService;
        private readonly IProductService _productService;
        private readonly ILoyaltyService _loyaltyService;

        public OrderBuyerController(
            ILogger<OrderBuyerController> logger,
            IOrderService orderService,
            IOrderItemService orderItemService,
            UserManager<User> userManager,
            INotificationService notificationService,
            IPushNotificationService pushNotificationService,
            IInventoryService inventoryService,
            InventoryDbContext inventoryContext,
            IAdService adService,
            IProductService productService,
            ILoyaltyService loyaltyService
        )
        {
            _logger = logger;
            _orderService = orderService;
            _orderItemService = orderItemService;
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _notificationService =
                notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _pushNotificationService =
                pushNotificationService
                ?? throw new ArgumentNullException(nameof(pushNotificationService));
            _inventoryService =
                inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _inventoryContext = inventoryContext;
            _adService = adService ?? throw new ArgumentNullException(nameof(adService));
            _productService =
                productService ?? throw new ArgumentNullException(nameof(productService));
            _loyaltyService = loyaltyService;
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
                _logger.LogWarning(
                    "[StoresController] CreateStore - Could not find user ID claim for the authenticated user."
                );
                return Unauthorized("User ID claim not found."); // 401 Unauthorized
            }

            _logger.LogInformation("Attempting to retrieve all orders of user.");
            try
            {
                var orders = await _orderService.GetOrdersByBuyerAsync(userId);

                var orderDtos = orders
                    .Select(o => new OrderGetBuyerDto
                    {
                        Id = o.Id,
                        BuyerId = o.BuyerId,
                        StoreId = o.StoreId,
                        Status = o.Status.ToString(),
                        Time = o.Time,
                        Total = o.Total,
                        OrderItems = o
                            .OrderItems.Select(oi => new OrderItemGetBuyerDto
                            {
                                Id = oi.Id,
                                ProductId = oi.ProductId,
                                Price = oi.Price,
                                Quantity = oi.Quantity,
                            })
                            .ToList(),
                        AddressId = o.AddressId,
                    })
                    .ToList();

                _logger.LogInformation(
                    "Successfully retrieved {OrderCount} orders.",
                    orderDtos.Count
                );
                return Ok(orderDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving all orders.");
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An internal error occurred while retrieving orders."
                );
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
                _logger.LogWarning(
                    "GetOrderById request failed validation: Invalid ID {OrderId}",
                    id
                );
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
                    OrderItems = order
                        .OrderItems.Select(oi => new OrderItemGetBuyerDto
                        {
                            Id = oi.Id,
                            ProductId = oi.ProductId,
                            Price = oi.Price,
                            Quantity = oi.Quantity,
                        })
                        .ToList(),
                    AddressId = order.AddressId,
                };

                _logger.LogInformation("Successfully retrieved order with ID: {OrderId}", id);
                return Ok(orderDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "An error occurred while retrieving order with ID: {OrderId}",
                    id
                );
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An internal error occurred while retrieving the order."
                );
            }
        }

        // POST /api/OrderBuyer/order/create
        [HttpPost("order/create")]
        [ProducesResponseType(typeof(OrderGetBuyerDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<OrderGetBuyerDto>> CreateOrder(
            [FromBody] OrderCreateBuyerDto createDto
        )
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning(
                    "[OrderBuyerController] CreateOrder - Could not find user ID claim for the authenticated user."
                );
                return Unauthorized("User ID claim not found."); // 401 Unauthorized
            }

            _logger.LogInformation(
                "Attempting to create a new order for BuyerId: {BuyerId}, StoreId: {StoreId}",
                userId,
                createDto.StoreId
            );

            if (!ModelState.IsValid)
            {
                _logger.LogWarning(
                    "Create order request failed model validation. Errors: {@ModelState}",
                    ModelState.Values.SelectMany(v => v.Errors)
                );
                return BadRequest(ModelState);
            }

            // Optional: Add validation to check if BuyerId and StoreId actually exist
            try
            {
                // Creating the order itself
                var createdOrder = await _orderService.CreateOrderAsync(
                    userId,
                    createDto.StoreId,
                    createDto.AddressId
                );
                List<OrderItemGetBuyerDto> listitems = new List<OrderItemGetBuyerDto>();

                // Process each order item
                var points = 0.0;
                foreach (var item in createDto.OrderItems)
                {
                    var createdItem = await _orderItemService.CreateOrderItemAsync(
                        createdOrder.Id,
                        item.ProductId,
                        item.Quantity
                    );

                    if (createdItem == null)
                    {
                        throw new Exception(
                            $"Failed to create order item for product {item.ProductId}."
                        );
                    }

                    // Biljezenje podataka za reklame

                    var product = await _productService.GetProductByIdAsync(item.ProductId);
                    points += ILoyaltyService.PointsForProduct(
                        product.RetailPrice,
                        item.Quantity,
                        product.PointRate
                    );

                    if (product == null)
                    {
                        throw new Exception($"Failed to find product with id {item.ProductId}.");
                    }

                    await _adService.CreateUserActivityAsync(
                        new UserActivity
                        {
                            Id = 0,
                            UserId = userId,
                            ProductCategoryId = product.ProductCategoryId,
                            TimeStamp = DateTime.Now,
                            InteractionType = InteractionType.Order,
                        }
                    );

                    // Add item to response DTO
                    listitems.Add(
                        new OrderItemGetBuyerDto
                        {
                            Id = createdItem.Id,
                            ProductId = createdItem.ProductId,
                            Price = createdItem.Price,
                            Quantity = createdItem.Quantity,
                        }
                    );

                    // Inventory update logic from complete OrderController
                    var inventoryList = await _inventoryService.GetInventoryAsync(
                        userId,
                        false,
                        item.ProductId,
                        createDto.StoreId
                    );
                    var inventory = inventoryList.FirstOrDefault();

                    if (inventory == null)
                    {
                        throw new InvalidOperationException(
                            $"Inventory not found for product ID {item.ProductId}."
                        );
                    }

                    var updateInvDto = new UpdateInventoryQuantityRequestDto
                    {
                        ProductId = item.ProductId,
                        StoreId = createDto.StoreId,
                        NewQuantity = inventory.Quantity - item.Quantity,
                    };

                    // Update inventory quantity
                    await _inventoryService.UpdateQuantityAsync(userId, false, updateInvDto);
                    _logger.LogInformation(
                        "Inventory updated for ProductId: {ProductId}, StoreId: {StoreId} after order item creation.",
                        item.ProductId,
                        createDto.StoreId
                    );
                }

                // Prepare Order DTO for response
                var orderDto = new OrderGetBuyerDto
                {
                    Id = createdOrder.Id,
                    BuyerId = createdOrder.BuyerId,
                    StoreId = createdOrder.StoreId,
                    Status = createdOrder.Status.ToString(),
                    Time = createdOrder.Time,
                    Total = createdOrder.Total, // Will likely be null or 0 initially
                    OrderItems = listitems, // Order items for the buyer
                    AddressId = createdOrder.AddressId,
                };

                // Sending notification to the seller
                var sellerUser = await _userManager.Users.FirstOrDefaultAsync(u =>
                    u.StoreId == createdOrder.StoreId
                );

                if (sellerUser != null)
                {
                    string notificationMessage =
                        $"Nova narudžba #{createdOrder.Id} je kreirana za vašu prodavnicu.";

                    await _notificationService.CreateNotificationAsync(
                        sellerUser.Id,
                        notificationMessage,
                        createdOrder.Id
                    );
                    _logger.LogInformation(
                        "Notification creation task initiated for Seller {SellerUserId} for new Order {OrderId}.",
                        sellerUser.Id,
                        createdOrder.Id
                    );

                    if (!string.IsNullOrWhiteSpace(sellerUser.FcmDeviceToken))
                    {
                        try
                        {
                            string pushTitle = "Nova Narudžba!";
                            string pushBody = $"Dobili ste narudžbu #{createdOrder.Id}.";
                            var pushData = new Dictionary<string, string>
                            {
                                { "orderId", createdOrder.Id.ToString() },
                                { "screen", "OrderDetail" }, // Example for frontend navigation
                            };

                            await _pushNotificationService.SendPushNotificationAsync(
                                sellerUser.FcmDeviceToken,
                                pushTitle,
                                pushBody,
                                pushData
                            );
                            _logger.LogInformation(
                                "Push Notification task initiated for Seller {SellerUserId} for new Order {OrderId}.",
                                sellerUser.Id,
                                createdOrder.Id
                            );
                        }
                        catch (Exception pushEx)
                        {
                            _logger.LogError(
                                pushEx,
                                "Failed to send Push Notification to Seller {SellerUserId} for Order {OrderId}.",
                                sellerUser.Id,
                                createdOrder.Id
                            );
                        }
                    }

                    if (createDto.UsingPoints)
                    {
                        var wallet = await _loyaltyService.GetUserPointsAsync(createdOrder.BuyerId);

                        var ptsQuantity = (int)
                            Math.Min(
                                (double)createdOrder.Total / LoyaltyRates.SellerPaysAdmin,
                                wallet
                            );
                        createdOrder.Total =
                            (createdOrder.Total ?? 0m)
                            - (ptsQuantity * (decimal)LoyaltyRates.SellerPaysAdmin);
                        await _loyaltyService.CreateTransaction(
                            createdOrder.Id,
                            createdOrder.BuyerId,
                            createdOrder.StoreId,
                            TransactionType.Spend,
                            ptsQuantity
                        );
                        await _orderService.SaveChange();
                    }
                    else
                    {
                        await _loyaltyService.CreateTransaction(
                            createdOrder.Id,
                            createdOrder.BuyerId,
                            createdOrder.StoreId,
                            TransactionType.Buy,
                            (int)points
                        );
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "Could not find seller user for StoreId {StoreId} to send new order notification for Order {OrderId}.",
                        createdOrder.StoreId,
                        createdOrder.Id
                    );
                }

                _logger.LogInformation(
                    "Successfully created order with ID: {OrderId}",
                    createdOrder.Id
                );
                return CreatedAtAction(
                    nameof(GetOrderById),
                    new { id = createdOrder.Id },
                    orderDto
                );
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to create order due to invalid arguments (BuyerId: {BuyerId}, StoreId: {StoreId})",
                    userId,
                    createDto.StoreId
                );
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "An error occurred while creating an order for BuyerId: {BuyerId}, StoreId: {StoreId}",
                    userId,
                    createDto.StoreId
                );
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An internal error occurred while creating the order."
                );
            }
        }
    }
}
