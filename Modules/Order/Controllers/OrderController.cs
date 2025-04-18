using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Order.Interface;

[Authorize(Roles = "Seller")]
[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrderController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    public async Task<IActionResult> GetOrdersForSeller()
    {
        var sellerUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(sellerUserId))
            return Unauthorized();

        var orders = await _orderService.GetOrdersForSellerAsync(sellerUserId);
        return Ok(orders);
    }
}
