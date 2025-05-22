using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Loyalty.Interfaces;
using Loyalty.Models;
using Loyalty.Services; // Za LoyaltyService (ako je PointsForProduct static)
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class LoyaltyController : ControllerBase
    {
        private readonly ILoyaltyService _loyaltyService;
        private readonly ILogger<LoyaltyController> _logger;

        public LoyaltyController(ILoyaltyService loyaltyService, ILogger<LoyaltyController> logger)
        {
            _loyaltyService = loyaltyService;
            _logger = logger;
        }

        // GET: api/loyalty/users/points/{userId}
        [HttpGet("users/points/{userId}")]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)] // Ako user ne postoji za kreiranje wallet-a
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<int>> GetUserPoints(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest("User ID cannot be empty.");
            }
            try
            {
                var points = await _loyaltyService.GetUserPointsAsync(userId);
                return Ok(points);
            }
            catch (ArgumentException ex) // Od CreateWalletForUserAsync ako user ne postoji
            {
                _logger.LogWarning(ex, "ArgumentException while getting points for user {UserId}: {ErrorMessage}", userId, ex.Message);
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Error getting user points for userId: {UserId}", userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving user points.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting user points for userId: {UserId}", userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // GET: api/loyalty/users/points/{userId}
        [HttpGet("users/points/my")]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)] // Ako user ne postoji za kreiranje wallet-a
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<int>> GetMyPoints()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("[OrderBuyerController] CreateOrder - Could not find user ID claim for the authenticated user.");
                return Unauthorized("User ID claim not found."); // 401 Unauthorized
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest("User ID cannot be empty.");
            }
            try
            {
                var points = await _loyaltyService.GetUserPointsAsync(userId);
                return Ok(points);
            }
            catch (ArgumentException ex) // Od CreateWalletForUserAsync ako user ne postoji
            {
                _logger.LogWarning(ex, "ArgumentException while getting points for user {UserId}: {ErrorMessage}", userId, ex.Message);
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Error getting user points for userId: {UserId}", userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving user points.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting user points for userId: {UserId}", userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // GET: api/loyalty/constants/admin-pays-seller
        [HttpGet("consts/admin/seller")]
        [ProducesResponseType(typeof(double), StatusCodes.Status200OK)]
        public ActionResult<double> GetAdminPaysSellerConst()
        {
            return Ok(_loyaltyService.GetAdminPaysSellerConst());
        }

        // GET: api/loyalty/constants/seller-pays-admin
        [HttpGet("constants/consts/seller/admin")]
        [ProducesResponseType(typeof(double), StatusCodes.Status200OK)]
        public ActionResult<double> GetSellerPaysAdminConst()
        {
            return Ok(_loyaltyService.GetSellerPaysAdminConst());
        }

        // GET: api/loyalty/constants/spending-point-rate
        [HttpGet("consts/spending")]
        [ProducesResponseType(typeof(double), StatusCodes.Status200OK)]
        public ActionResult<double> GetSpendingPointRateConst()
        {
            return Ok(_loyaltyService.GetSpendingPointRateConst());
        }

        // GET: api/loyalty/admin/income
        [HttpGet("admin/income")]
        [ProducesResponseType(typeof(double), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<double>> GetAdminIncome([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] List<int>? storeIds)
        {
            // Napomena: Vaš servis ima bug, `transactions` lista ostaje prazna.
            // `await _context.Transactions.Where(...).ToListAsync();` treba biti `transactions = await _context.Transactions.Where(...).ToListAsync();`
            // Ovaj kontroler će raditi kako je servis napisan.
            try
            {
                var income = await _loyaltyService.GetAdminIncomeAsync(from, to, storeIds);
                return Ok(income);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting admin income. From: {From}, To: {To}, StoreIds: {StoreIds}", from, to, string.Join(",", storeIds ?? new List<int>()));
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while calculating admin income.");
            }
        }

        // GET: api/loyalty/admin/profit
        [HttpGet("admin/profit")]
        [ProducesResponseType(typeof(double), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<double>> GetAdminProfit([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] List<int>? storeIds)
        {
            // Napomena: Isti bug kao u GetAdminIncomeAsync.
            try
            {
                var profit = await _loyaltyService.GetAdminProfitAsync(from, to, storeIds);
                return Ok(profit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting admin profit. From: {From}, To: {To}, StoreIds: {StoreIds}", from, to, string.Join(",", storeIds ?? new List<int>()));
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while calculating admin profit.");
            }
        }

        // GET: api/loyalty/store/{storeId}/income
        [HttpGet("store/{storeId}/income")]
        [ProducesResponseType(typeof(double), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<double>> GetStoreIncome(int storeId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            // Napomena: Isti bug kao u GetAdminIncomeAsync.
            try
            {
                var income = await _loyaltyService.GetStoreIncomeAsync(storeId, from, to);
                return Ok(income);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting store income for storeId: {StoreId}. From: {From}, To: {To}", storeId, from, to);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while calculating store income.");
            }
        }
    }
}