public interface IReviewReminderService
{
    Task SendReminderAsync(string buyerUserId, int orderId);
}
