using Microsoft.AspNetCore.Identity;
using Notifications.Interfaces;
using Order.Interface;
using Order.Services;
using Users.Models;

namespace Notifications.Services
{
    public class ReviewReminderService : IReviewReminderService
    {
        private readonly INotificationService _notificationService;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly IOrderService _orderService;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<OrderService> _logger;

        public ReviewReminderService(
            INotificationService notificationService,
            IPushNotificationService pushNotificationService,
            IOrderService orderService,
            UserManager<User> userManager,
            ILogger<OrderService> logger
            )
        {
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _pushNotificationService = pushNotificationService ?? throw new ArgumentNullException(nameof(notificationService)); 
            _orderService = orderService; 
            _userManager = userManager;
            _logger = logger;
        }

        // Metoda koja šalje podsjetnik
        public async Task SendReminderAsync(string buyerUserId, int orderId)
        {
            // Sending notification to the buyer
            var order = await _orderService.GetOrderByIdAsync(orderId);
            var buyerUser = await _userManager.FindByIdAsync(buyerUserId);

            if (buyerUser != null && order != null)
            {
                string notificationMessage = $"Podsjećamo Vas da ostavite recenziju za Vašu nedavnu narudžbu.";

                await _notificationService.CreateNotificationAsync(
                    buyerUser.Id,
                    notificationMessage,
                    order.Id
                );
                _logger.LogInformation("Notification creation task initiated for Buyer {BuyerUserId} for Order {OrderId}.", buyerUser.Id, order.Id);

                if (!string.IsNullOrWhiteSpace(buyerUser.FcmDeviceToken))
                {
                    try
                    {
                        string pushTitle = "Ostavite recenziju!";
                        string pushBody = $"Završena je narudžbu #{order.Id}.";
                        var pushData = new Dictionary<string, string>
                    {
                        { "orderId", order.Id.ToString() },
                        { "screen", "OrderDetail" } // Example for frontend navigation
                    };
                        
                        // Pošaljite notifikaciju
                        await _pushNotificationService.SendPushNotificationAsync(
                            buyerUser.FcmDeviceToken,
                            pushTitle,
                            pushBody,
                            pushData
                        );
                        _logger.LogInformation("Push Notification task initiated for Buyer {BuyerUserId} for Order {OrderId}.", buyerUser.Id, order.Id);
                    }
                    catch (Exception pushEx)
                    {
                        _logger.LogError(pushEx, "Failed to send Push Notification to Buyer {BuyerUserId} for Order {OrderId}.", buyerUser.Id, order.Id);
                    }
                }

            }
            else
            {
                _logger.LogWarning("Cannot send reminder: buyerUser or order is null. BuyerUserId: {BuyerId}, OrderId: {OrderId}.", buyerUser?.Id, order?.Id);
            }
        }
    }
}

