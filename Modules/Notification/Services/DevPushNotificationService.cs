using FirebaseAdmin.Messaging;
using Notifications.Interfaces;

namespace Notifications.Services
{
    public class DevPushNotificationService : IPushNotificationService
    {
        private readonly ILogger<DevPushNotificationService> _logger;
        public DevPushNotificationService(ILogger<DevPushNotificationService> logger)
        {
            _logger = logger;
        }
        public async Task<bool> SendPushNotificationAsync(string userFcmToken, string title, string body, Dictionary<string, string>? data = null)
        {

            if (string.IsNullOrWhiteSpace(userFcmToken))
            {
                _logger.LogWarning("Cannot send push notification: FCM Device Token is missing for the target user.");
                return false;
            }

            _logger.LogInformation("Attempting to send push notification to token {Token} - Title: {Title}", userFcmToken, title);


            _logger.LogInformation($"""
            Message
                Notification = new Notification
                    
                        Title = {title},
                        Body = {body},
                    ,
                Data={data}
                Token={userFcmToken}
            """);

            return true;

        }
    }
}