namespace Notifications.Interfaces
{
    public interface IPushNotificationService
    {
        public Task<bool> SendPushNotificationAsync(string userFcmToken, string title, string body, Dictionary<string, string>? data = null);
    }
}