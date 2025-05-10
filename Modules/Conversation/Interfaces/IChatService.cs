
using Chat.Dtos;

namespace Chat.Interfaces
{
    public interface IChatService
    {
        Task<ConversationDto?> GetOrCreateConversationAsync(string requestingUserId, string targetUserId, int storeId, int? orderId = null, int? productId = null);

        Task<MessageDto?> SaveMessageAsync(string senderUserId, int conversationId, string content, bool isPrivate);
        Task<IEnumerable<ConversationDto>> GetConversationsForUserAsync(string userId);
        Task<IEnumerable<MessageDto>> GetConversationMessagesAsync(int conversationId, string requestingUserId, bool isAdmin, int page = 1, int pageSize = 30);
        Task<bool> CanUserAccessConversationAsync(string userId, int conversationId);
        Task<bool> MarkMessagesAsReadAsync(int conversationId, string readerUserId);
        Task<IEnumerable<MessageDto>> GetAllMessagesForConversationAsync(int conversationId, string requestingUserId, bool isAdmin, int page = 1, int pageSize = 30);
    }
}