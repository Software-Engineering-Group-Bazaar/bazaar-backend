using Notifications.Interfaces;
using System;
using System.Threading.Tasks;

namespace Notifications.Services
{
    public class ReviewReminderService
    {
        private readonly INotificationService _notificationService;

        public ReviewReminderService(INotificationService notificationService)
        {
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        }

        // Metoda koja šalje podsjetnik s odgodom
        public async Task SendReminderWithDelayAsync(string userId, int orderId)
        {
            // Odgodi 3 minute (180,000 ms)
            await Task.Delay(TimeSpan.FromMinutes(3));

            // Pošaljite notifikaciju
            var message = "Podsjećamo vas da ostavite recenziju za vašu nedavnu narudžbu.";
            await _notificationService.CreateNotificationAsync(userId, message, orderId);
        }
    }
}

