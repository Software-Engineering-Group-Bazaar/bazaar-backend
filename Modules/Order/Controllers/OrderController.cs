using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Catalog.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Order.DTOs;
using Order.Interface;
using Order.Models; // 

namespace Order.Controllers
{
    [ApiController]
    [Route("api/order")] // Osnovna ruta
    // [Authorize] // Možemo staviti osnovnu autorizaciju na nivou kontrolera
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly IOrderItemService _orderItemService; // Dodajemo OrderItemService
        private readonly ILogger<OrderController> _logger;

        // Injectujemo oba servisa
        public OrderController(IOrderService orderService, IOrderItemService orderItemService, ILogger<OrderController> logger)
        {
            _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
            _orderItemService = orderItemService ?? throw new ArgumentNullException(nameof(orderItemService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // === Endpointi za Narudžbe (Orders) ===

        // GET /api/order - Dohvati narudžbe (za Seller-a ili Admin-a?)
        // POSTOJEĆI (za Seller-a)
        [HttpGet]
        [Authorize(Roles = "Admin,Seller,Buyer")] // Samo Seller vidi svoje narudžbe preko ovog endpointa
        [ProducesResponseType(typeof(IEnumerable<OrderSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetOrdersForSeller()
        {
            var sellerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(sellerUserId)) return Unauthorized("User ID claim not found.");

            try
            {
                var orders = await _orderService.GetOrdersForSellerAsync(sellerUserId);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving orders for seller {SellerUserId}", sellerUserId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving orders.");
            }
        }

        // GET /api/order/all - Dohvati SVE narudžbe 
        [HttpGet("all")]
        [Authorize(Roles = "Admin,Seller,Buyer")]
        [ProducesResponseType(typeof(IEnumerable<OrderModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllOrders()
        {
            _logger.LogInformation("[Admin] Attempting to retrieve all orders.");
            try
            {
                var orders = await _orderService.GetAllOrdersAsync();
                // TODO: Mapirati u odgovarajući DTO ako ne želimo vraćati pune modele
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] Error retrieving all orders.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred retrieving orders.");
            }
        }

        // GET /api/order/{orderId} - Dohvati detalje specifične narudžbe
        [HttpGet("{orderId:int}")]
        [Authorize(Roles = "Seller,Admin,Buyer")]
        [ProducesResponseType(typeof(OrderModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)] // Ako user nema pristup ovoj narudžbi
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetOrderDetails(int orderId)
        {
            if (orderId <= 0) return BadRequest("Invalid Order ID.");

            var requestingUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRoles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            if (string.IsNullOrEmpty(requestingUserId)) return Unauthorized();

            _logger.LogInformation("User {UserId} retrieving details for Order {OrderId}", requestingUserId, orderId);

            try
            {
                var order = await _orderService.GetOrderByIdAsync(orderId); // Dohvati order
                if (order == null) return NotFound($"Order with ID {orderId} not found.");

                return Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving details for Order {OrderId}", orderId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred retrieving order details.");
            }
        }

        // POST /api/order - Kreiranje nove narudžbe (radi Buyer?)
        // Metoda CreateOrderAsync prima buyerId i storeId. Pretpostavljam da ovo poziva Buyer.
        [HttpPost]
        [Authorize(Roles = "Admin,Seller,Buyer")] // Samo Buyer može kreirati narudžbu?
        [ProducesResponseType(typeof(OrderModel), StatusCodes.Status201Created)] // Vraća model? Treba DTO.
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequestDto createDto) // Treba DTO za ovo
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var buyerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(buyerUserId)) return Unauthorized();

            _logger.LogInformation("Buyer {BuyerId} attempting to create order for Store {StoreId}", buyerUserId, createDto.StoreId);

            try
            {
                // TODO: Validacija da li StoreId postoji? Da li Buyer može naručiti?
                // Servis prima int ID, ne string
                if (!int.TryParse(buyerUserId, out int buyerIdInt)) // Ovo neće raditi ako User ID nije int! Identity User ID je string (GUID ili int)
                {
                    _logger.LogError("Could not parse Buyer User ID '{BuyerUserId}' to int.", buyerUserId);
                    return StatusCode(StatusCodes.Status500InternalServerError, "User ID format error.");
                }

                var createdOrder = await _orderService.CreateOrderAsync(buyerIdInt, createDto.StoreId);

                return CreatedAtAction(nameof(GetOrderDetails), new { orderId = createdOrder.Id }, createdOrder);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order for Buyer {BuyerId}, Store {StoreId}", buyerUserId, createDto.StoreId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the order.");
            }
        }

        // POST /api/order/status - Ažuriraj status narudžbe (za Sellera)
        [HttpPost("status")]
        [Authorize(Roles = "Admin,Seller,Buyer")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateOrderStatus([FromBody] UpdateOrderStatusRequestDto updateDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var sellerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(sellerUserId)) return Unauthorized("User ID claim not found.");

            try
            {
                // Koristi novu metodu servisa koja radi autorizaciju
                var success = await _orderService.UpdateOrderStatusForSellerAsync(sellerUserId, updateDto);
                if (!success) return NotFound($"Order with ID {updateDto.OrderId} not found or status update failed.");
                return NoContent();
            }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); } // Ili Conflict(409)?
            catch (UnauthorizedAccessException ex) { _logger.LogWarning(ex, "Forbidden status update attempt by {UserId} for Order {OrderId}", sellerUserId, updateDto.OrderId); return Forbid(); }
            catch (Exception ex) { _logger.LogError(ex, "Error updating status for Order {OrderId} by User {UserId}.", updateDto.OrderId, sellerUserId); return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred."); }
        }

        // DELETE /api/order/{orderId} - Brisanje narudžbe
        [HttpDelete("{orderId:int}")]
        [Authorize(Roles = "Admin,Seller,Buyer")] // Samo Admin briše narudžbe?
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteOrder(int orderId)
        {
            if (orderId <= 0) return BadRequest("Invalid Order ID.");
            _logger.LogInformation("[Admin] Attempting to delete Order {OrderId}", orderId);
            try
            {
                var success = await _orderService.DeleteOrderAsync(orderId);
                if (!success) return NotFound($"Order with ID {orderId} not found.");
                return NoContent();
            }
            catch (Exception ex) // Npr. DbUpdateException
            {
                _logger.LogError(ex, "[Admin] Error deleting Order {OrderId}", orderId);
                // Možda Conflict(409) ako postoje reference?
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the order.");
            }
        }


        // === Endpointi za Stavke Narudžbe (OrderItems) ===
        // Ove endpoint-e stavljamo ovdje za sada radi jednostavnosti

        // GET /api/order/{orderId}/items - Dohvati sve stavke za narudžbu
        [HttpGet("{orderId:int}/items")]
        [Authorize(Roles = "Seller,Admin,Buyer")] // Provjeriti ko smije vidjeti stavke
        [ProducesResponseType(typeof(IEnumerable<OrderItem>), StatusCodes.Status200OK)] // Treba DTO
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)] // Ako order ne postoji
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetItemsForOrder(int orderId)
        {
            if (orderId <= 0) return BadRequest("Invalid Order ID.");
            // TODO: Dodati autorizaciju sličnu kao u GetOrderDetails da se osigura da user smije vidjeti ovaj order
            _logger.LogInformation("Retrieving items for Order {OrderId}", orderId);
            try
            {
                // Provjeri prvo da li order postoji i da li user ima pristup
                var orderExists = await _orderService.GetOrderByIdAsync(orderId); // Brza provjera
                if (orderExists == null) return NotFound($"Order with ID {orderId} not found.");
                // Ovdje ide puna autorizaciona logika kao u GetOrderDetails...

                var items = await _orderItemService.GetOrderItemsByOrderIdAsync(orderId);
                // TODO: Mapirati u OrderItemDto
                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving items for Order {OrderId}", orderId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred retrieving order items.");
            }
        }

        // POST /api/order/{orderId}/items - Dodaj novu stavku u narudžbu (radi Buyer?)
        [HttpPost("{orderId:int}/items")]
        [Authorize(Roles = "Admin,Seller,Buyer")] // Samo Buyer dodaje iteme?
        [ProducesResponseType(typeof(OrderItem), StatusCodes.Status201Created)] // Treba DTO
        [ProducesResponseType(StatusCodes.Status400BadRequest)] // Nevažeći ID-jevi, količina, nepostojeći proizvod...
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)] // Ako Buyer nije vlasnik narudžbe
        [ProducesResponseType(StatusCodes.Status404NotFound)] // Ako order ne postoji
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AddItemToOrder(int orderId, [FromBody] CreateOrderItemDto createDto) // Treba DTO
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (orderId <= 0) return BadRequest("Invalid Order ID.");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            _logger.LogInformation("User {UserId} attempting to add Product {ProductId} (Qty: {Quantity}) to Order {OrderId}",
                                   userId, createDto.ProductId, createDto.Quantity, orderId);
            try
            {
                // TODO: Autorizacija - provjeri da li userId posjeduje Order sa orderId
                var order = await _orderService.GetOrderByIdAsync(orderId);
                if (order == null) return NotFound($"Order with ID {orderId} not found.");
                if (order.BuyerId.ToString() != userId) return Forbid("Cannot add items to another user's order."); // Pazi na tip ID-ja!

                // TODO: Validacija - Da li je status narudžbe 'Requested'? Ne može se dodavati u poslate/završene.
                if (order.Status != OrderStatus.Requested) return BadRequest("Cannot add items to an order that is not in 'Requested' status.");

                // Servis prima samo ID-eve i količinu
                var newItem = await _orderItemService.CreateOrderItemAsync(orderId, createDto.ProductId, createDto.Quantity);

                // TODO: Mapirati newItem u OrderItemDto za odgovor
                // Vratiti lokaciju? Možda GET /api/order/items/{itemId} ?
                return CreatedAtAction(nameof(GetItemsForOrder), new { orderId = orderId }, newItem); // Vraća model, treba DTO
            }
            catch (ArgumentException ex) // Npr. nepostojeći ProductId, neaktivni proizvod, negativna količina...
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex) // Npr. Order ne postoji (iz servisa)
            {
                return NotFound(new { message = ex.Message }); // Vrati 404 ako servis javi da order ne postoji
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item (Product {ProductId}) to Order {OrderId}", createDto.ProductId, orderId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while adding the item.");
            }
        }

        // PUT /api/order/items/{orderItemId} - Ažuriraj stavku narudžbe (količinu?)
        [HttpPut("items/{orderItemId:int}")]
        [Authorize(Roles = "Admin,Seller,Buyer")] // Samo Buyer mijenja svoju narudžbu?
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)] // Nevažeći ID-jevi, količina...
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)] // Ako Buyer nije vlasnik
        [ProducesResponseType(StatusCodes.Status404NotFound)] // Ako item ili order ne postoji
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateOrderItem(int orderItemId, [FromBody] UpdateOrderItemDto updateDto) // Treba DTO
        {
            if (orderItemId <= 0) return BadRequest("Invalid Order Item ID.");
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            _logger.LogInformation("User {UserId} attempting to update OrderItem {OrderItemId} to Qty {Quantity}, Product {ProductId}",
                                   userId, orderItemId, updateDto.Quantity, updateDto.ProductId);

            try
            {
                // TODO: Autorizacija - Dohvati OrderItem, pa njegov Order, pa provjeri BuyerId vs userId
                var item = await _orderItemService.GetOrderItemByIdAsync(orderItemId);
                if (item == null) return NotFound($"Order item with ID {orderItemId} not found.");
                var order = await _orderService.GetOrderByIdAsync(item.OrderId);
                if (order == null || order.BuyerId.ToString() != userId) return Forbid("Cannot modify items in another user's order."); // Pazi na tip ID-ja

                // TODO: Validacija - Da li je status narudžbe 'Requested'?
                if (order.Status != OrderStatus.Requested) return BadRequest("Cannot modify items in an order that is not in 'Requested' status.");

                // Proslijedi podatke servisu
                var success = await _orderItemService.UpdateOrderItemAsync(orderItemId, updateDto.Quantity, updateDto.ProductId);

                if (!success)
                {
                    // Može biti NotFound za item, ili greška pri update-u
                    return NotFound($"Order item with ID {orderItemId} not found or update failed.");
                }
                return NoContent();
            }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating OrderItem {OrderItemId}", orderItemId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the item.");
            }
        }

        // DELETE /api/order/items/{orderItemId} - Obriši stavku iz narudžbe
        [HttpDelete("items/{orderItemId:int}")]
        [Authorize(Roles = "Admin,Seller,Buyer")] // Samo Buyer briše iz svoje narudžbe?
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteOrderItem(int orderItemId)
        {
            if (orderItemId <= 0) return BadRequest("Invalid Order Item ID.");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            _logger.LogInformation("User {UserId} attempting to delete OrderItem {OrderItemId}", userId, orderItemId);

            try
            {
                // TODO: Autorizacija - Dohvati OrderItem, pa njegov Order, pa provjeri BuyerId vs userId
                var item = await _orderItemService.GetOrderItemByIdAsync(orderItemId);
                if (item == null) return NotFound($"Order item with ID {orderItemId} not found.");
                var order = await _orderService.GetOrderByIdAsync(item.OrderId);
                if (order == null || order.BuyerId.ToString() != userId) return Forbid("Cannot delete items from another user's order."); // Pazi na tip ID-ja

                // TODO: Validacija - Da li je status narudžbe 'Requested'?
                if (order.Status != OrderStatus.Requested) return BadRequest("Cannot delete items from an order that is not in 'Requested' status.");

                var success = await _orderItemService.DeleteOrderItemAsync(orderItemId);
                if (!success)
                {
                    return NotFound($"Order item with ID {orderItemId} not found or deletion failed.");
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting OrderItem {OrderItemId}", orderItemId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the item.");
            }
        }
    }
}