using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Order.DTOs;
using Order.Interface;

[Authorize(Roles = "Seller")]
[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrderController(IOrderService orderService)
    {
        _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
    }

    // GET /api/order - Dohvati narudžbe za ulogovanog Sellera
    [HttpGet]
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
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving orders.");
        }
    }

    // POST /api/order/status - Ažuriraj status narudžbe
    [HttpPost("status")] // Ruta za ažuriranje statusa
    [ProducesResponseType(StatusCodes.Status204NoContent)] // Uspjeh bez sadržaja
    [ProducesResponseType(StatusCodes.Status400BadRequest)]  // Nevažeći status ili tranzicija
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]  // Nije vlasnik narudžbe/prodavnice
    [ProducesResponseType(StatusCodes.Status404NotFound)]  // Narudžba nije pronađena
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateOrderStatus([FromBody] UpdateOrderStatusRequestDto updateDto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var sellerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(sellerUserId)) return Unauthorized("User ID claim not found.");

        try
        {
            var success = await _orderService.UpdateOrderStatusForSellerAsync(sellerUserId, updateDto);

            if (!success)
            {
                // Servis vraća false ako order nije nađen
                return NotFound($"Order with ID {updateDto.OrderId} not found.");
            }

            return NoContent(); // Uspješno ažurirano
        }
        catch (ArgumentException ex) // Npr. nevažeći status string
        {

            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) // Npr. nedozvoljena tranzicija statusa
        {
            // Može biti i 409 Conflict ovisno o značenju
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex) // Ako Seller nije vlasnik
        {

            return Forbid(); // 403 Forbidden
        }
        catch (Exception ex) // Ostale greške
        {
            return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the order status.");
        }
    }

}