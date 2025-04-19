using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Notifications.Dtos;
using Notifications.Interfaces;


namespace Notifications.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(INotificationService notificationService, ILogger<NotificationsController> logger)
        {
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }


        [HttpPost("create")]
        [Authorize(Roles = "Admin,Seller")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateTestNotification([FromBody] CreateNotificationRequestDto request)
        {
            var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Log who initiated

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                await _notificationService.CreateNotificationAsync(
                    request.UserId,
                    request.Message
                );

                return StatusCode(StatusCodes.Status201Created, new { message = "Test notification created successfully." });
            }
            catch (ArgumentNullException ex)
            {
                _logger.LogWarning(ex, "Invalid argument creating notification for User {UserId}", request.UserId);
                return BadRequest(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument creating notification for User {UserId}", request.UserId);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notification for User {UserId}.", request.UserId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while creating the test notification.");
            }
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<NotificationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetMyNotifications(
            [FromQuery] bool onlyUnread = true,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            if (pageSize > 100) pageSize = 100;
            if (pageSize < 1) pageSize = 1;
            if (pageNumber < 1) pageNumber = 1;

            _logger.LogInformation("User {UserId} fetching notifications. UnreadOnly: {OnlyUnread}, Page: {PageNumber}, Size: {PageSize}",
                                   userId, onlyUnread, pageNumber, pageSize);

            try
            {
                var notifications = await _notificationService.GetNotificationsForUserAsync(userId, onlyUnread, pageNumber, pageSize);
                return Ok(notifications ?? Enumerable.Empty<NotificationDto>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving notifications for User {UserId}.", userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving notifications.");
            }
        }

        [HttpGet("unread-count")]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetMyUnreadNotificationCount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            _logger.LogInformation("User {UserId} fetching unread notification count.", userId);

            try
            {
                var count = await _notificationService.GetUnreadNotificationCountAsync(userId);
                return Ok(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unread notification count for User {UserId}.", userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving notification count.");
            }
        }

        [HttpPost("mark-read")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> MarkAllMyNotificationsAsRead()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            _logger.LogInformation("User {UserId} marking all notifications as read.", userId);

            try
            {
                await _notificationService.MarkNotificationsAsReadAsync(userId); // Poziva bez liste ID-jeva
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read for User {UserId}.", userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while marking notifications as read.");
            }
        }

        [HttpPost("{notificationId:int}/mark-read")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> MarkNotificationAsRead(int notificationId)
        {
            if (notificationId <= 0) return BadRequest("Invalid Notification ID.");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            _logger.LogInformation("User {UserId} marking notification {NotificationId} as read.", userId, notificationId);

            try
            {
                var success = await _notificationService.MarkNotificationsAsReadAsync(userId, new List<int> { notificationId });
                if (!success)
                {
                    _logger.LogWarning("Failed to mark notification {NotificationId} as read for User {UserId}. It might not exist, not belong to the user, or already be read.", notificationId, userId);
                    return NotFound($"Notification with ID {notificationId} not found or could not be marked as read for this user.");
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification {NotificationId} as read for User {UserId}.", notificationId, userId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while marking the notification as read.");
            }
        }
    }
}