public interface IReviewReminderService
{
    Task SendReminderAsync(string userId);
}
