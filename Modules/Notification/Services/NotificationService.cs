using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notifications.Dtos;
using Notifications.Interfaces;
using Notifications.Models;

namespace Notifications.Services
{
    public class NotificationService : INotificationService
    {
        private readonly NotificationsDbContext _context;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(NotificationsDbContext context, ILogger<NotificationService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task CreateNotificationAsync(string userId, string message, int? orderId = null)
        {
            if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));
            if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Message cannot be empty.", nameof(message));

            var notification = new Notification
            {
                UserId = userId,
                Message = message,
                IsRead = false,
                Timestamp = DateTime.UtcNow,
                OrderId = orderId
            };

            try
            {
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Created notification ID {NotificationId} for User {UserId}. OrderId: {OrderId}",
                    notification.Id, userId, orderId.HasValue ? orderId.Value.ToString() : "N/A"); // Adjusted log
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating notification for User {UserId}.", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Generic error creating notification for User {UserId}.", userId);
            }
        }

        public async Task<IEnumerable<NotificationDto>> GetNotificationsForUserAsync(string userId, bool onlyUnread = true, int pageNumber = 1, int pageSize = 10)
        {
            if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var query = _context.Notifications
                              .Where(n => n.UserId == userId)
                              .OrderByDescending(n => n.Timestamp)
                              .AsNoTracking();

            if (onlyUnread)
            {
                query = query.Where(n => !n.IsRead);
            }

            var notifications = await query
                                     .Skip((pageNumber - 1) * pageSize)
                                     .Take(pageSize)
                                     .ToListAsync();

            return notifications.Select(n => new NotificationDto
            {
                Id = n.Id,
                Message = n.Message,
                IsRead = n.IsRead,
                Timestamp = n.Timestamp,
                OrderId = n.OrderId
            }).ToList();
        }

        public async Task<int> GetUnreadNotificationCountAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));

            return await _context.Notifications
                                 .CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        public async Task<bool> MarkNotificationsAsReadAsync(string userId, IEnumerable<int>? notificationIds = null)
        {
            if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));

            int updatedCount = 0;
            try
            {
                if (notificationIds == null || !notificationIds.Any())
                {
                    // Mark all unread as read
                    _logger.LogInformation("Marking all unread notifications as read for User {UserId}", userId);
                    updatedCount = await _context.Notifications
                                                 .Where(n => n.UserId == userId && !n.IsRead)
                                                 .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true));
                }
                else
                {
                    // Mark specific IDs as read
                    var validIds = notificationIds.Where(id => id > 0).Distinct().ToList();
                    _logger.LogInformation("Marking notifications with IDs [{NotificationIds}] as read for User {UserId}", string.Join(",", validIds), userId);
                    if (!validIds.Any()) return true; // Nothing to mark

                    updatedCount = await _context.Notifications
                                                .Where(n => n.UserId == userId && !n.IsRead && validIds.Contains(n.Id))
                                                .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true));
                }

                _logger.LogInformation("Marked {Count} notifications as read for User {UserId}", updatedCount, userId);
                return updatedCount >= 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notifications as read for User {UserId}", userId);
                return false;
            }
        }
    }
}