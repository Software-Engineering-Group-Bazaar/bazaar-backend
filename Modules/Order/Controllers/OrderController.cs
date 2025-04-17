using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Order.Interfaces;
using Order.Models.DTOs;

namespace Order.Controllers
{
    [ApiController]
    [Route("api/order")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrderController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        /// <summary>
        /// Ažurira status narudžbe za autentifikovanog Sellera.
        /// </summary>
        /// <param name="request">Podaci o narudžbi i novom statusu</param>
        /// <returns>HTTP odgovor sa statusom operacije</returns>
        [Authorize(Roles = "Seller")]
        [HttpPost("status")]
        public async Task<IActionResult> UpdateOrderStatus([FromBody] UpdateOrderStatusRequestDto request)
        {
            // Izvuci seller-ov userId iz JWT tokena
            var sellerUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(sellerUserId))
                return Unauthorized("Nije moguće pronaći identitet korisnika.");

            var updateResult = await _orderService.UpdateOrderStatusAsync(sellerUserId, request.OrderId, request.NewStatus);

            if (!updateResult)
                return BadRequest("Neuspješno ažuriranje statusa narudžbe. Provjerite da li narudžba postoji, pripada vašoj prodavnici i da li je status validan.");

            return Ok(new { message = "Status narudžbe je uspješno ažuriran." });
        }
    }
}
