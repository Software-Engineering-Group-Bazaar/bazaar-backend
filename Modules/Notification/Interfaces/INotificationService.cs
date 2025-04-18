using System.Collections.Generic;
using System.Threading.Tasks;
using Notifications.Dtos;

namespace Notifications.Interfaces
{
    public interface INotificationService
    {
        Task CreateNotificationAsync(string userId, string message, string? relatedEntityType = null, int? relatedEntityId = null, string? linkUrl = null);

        Task<IEnumerable<NotificationDto>> GetNotificationsForUserAsync(string userId, bool onlyUnread = true, int pageNumber = 1, int pageSize = 10);

        Task<int> GetUnreadNotificationCountAsync(string userId);

        Task<bool> MarkNotificationsAsReadAsync(string userId, IEnumerable<int>? notificationIds = null);
    }
}