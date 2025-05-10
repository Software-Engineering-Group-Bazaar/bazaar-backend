using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Catalog.Models; // Za Product model
using Catalog.Models;   // Za CatalogDbContext
using Chat.Dtos;
using Chat.Interfaces;
using Conversation.Data; // Koristiš Conversation.Data
using Conversation.Models; // Koristiš Conversation.Models
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Store.Interface;
using Store.Models;
using Users.Models;

namespace Chat.Services
{
    public class ChatService : IChatService
    {
        private readonly ConversationDbContext _context; // Koristiš ConversationDbContext
        private readonly UserManager<User> _userManager;
        private readonly IStoreService _storeService;
        private readonly CatalogDbContext _catalogContext; // Za ProductName
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            ConversationDbContext context, // Koristiš ConversationDbContext
            UserManager<User> userManager,
            IStoreService storeService,
            CatalogDbContext catalogContext, // Inject
            ILogger<ChatService> logger)
        {
            _context = context;
            _userManager = userManager;
            _storeService = storeService;
            _catalogContext = catalogContext; // Assign
            _logger = logger;
        }

        // Metoda GetOrCreateConversationAsync (ostaje uglavnom ista, samo pazi na ime DbContext-a)
        public async Task<ConversationDto?> GetOrCreateConversationAsync(
            string requestingUserId, string targetUserId, int storeId, int? orderId = null, int? productId = null)
        {
            // ... (Validacija: Ne mogu i OrderId i ProductId biti postavljeni) ...
            // ... (Logika za određivanje BuyerId i SellerId) ...
            _logger.LogInformation("GetOrCreateConversation: RequestingUser: {RU}, TargetUser: {TU}, Store: {S}, Order: {O}, Product: {P}",
               requestingUserId, targetUserId, storeId, orderId?.ToString() ?? "N/A", productId?.ToString() ?? "N/A");

            if (orderId.HasValue && productId.HasValue)
            {
                _logger.LogWarning("GetOrCreateConversation failed: Both OrderId and ProductId were provided.");
                throw new ArgumentException("A conversation can be related to an Order or a Product, but not both.");
            }

            var requestingUserObj = await _userManager.FindByIdAsync(requestingUserId);
            var targetUserObj = await _userManager.FindByIdAsync(targetUserId);

            if (requestingUserObj == null) throw new KeyNotFoundException($"Requesting user {requestingUserId} not found.");
            if (targetUserObj == null) throw new KeyNotFoundException($"Target user {targetUserId} not found.");

            string buyerId, sellerId;
            if (targetUserObj.StoreId.HasValue && targetUserObj.StoreId.Value == storeId)
            {
                sellerId = targetUserObj.Id;
                buyerId = requestingUserObj.Id;
                if (requestingUserObj.StoreId.HasValue && requestingUserObj.StoreId.Value == storeId && requestingUserObj.Id == targetUserObj.Id)
                {
                    throw new InvalidOperationException("Seller cannot create a conversation with themselves for their own store.");
                }
            }
            else if (requestingUserObj.StoreId.HasValue && requestingUserObj.StoreId.Value == storeId)
            {
                sellerId = requestingUserObj.Id;
                buyerId = targetUserObj.Id;
            }
            else
            {
                _logger.LogError("Could not determine Buyer/Seller roles for conversation between {User1} and {User2} regarding Store {StoreId}.", requestingUserId, targetUserId, storeId);
                throw new InvalidOperationException("Could not establish a valid buyer-seller relationship for this store.");
            }

            var query = _context.Conversations.AsQueryable();
            query = query.Where(c =>
                ((c.BuyerUserId == buyerId && c.SellerUserId == sellerId) || (c.BuyerUserId == sellerId && c.SellerUserId == buyerId)) &&
                c.StoreId == storeId);

            if (orderId.HasValue) query = query.Where(c => c.OrderId == orderId.Value && c.ProductId == null);
            else if (productId.HasValue) query = query.Where(c => c.ProductId == productId.Value && c.OrderId == null);
            else query = query.Where(c => c.OrderId == null && c.ProductId == null);

            var existingConversation = await query.FirstOrDefaultAsync();

            if (existingConversation != null)
            {
                _logger.LogInformation("Found existing conversation ID: {ConversationId}", existingConversation.Id);
                return await MapConversationToDtoAsync(existingConversation, requestingUserId);
            }

            _logger.LogInformation("Creating new conversation. Buyer: {B}, Seller: {S}, Store: {Store}, Order: {O}, Product: {P}",
                buyerId, sellerId, storeId, orderId?.ToString() ?? "N/A", productId?.ToString() ?? "N/A");

            var newConversation = new Conversation.Models.Conversation // Koristi puno ime zbog namespace-a
            {
                BuyerUserId = buyerId,
                SellerUserId = sellerId,
                StoreId = storeId,
                OrderId = orderId,
                ProductId = productId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Conversations.Add(newConversation);
            await _context.SaveChangesAsync();
            _logger.LogInformation("New conversation created with ID: {ConversationId}", newConversation.Id);

            return await MapConversationToDtoAsync(newConversation, requestingUserId);
        }


        // SaveMessageAsync ostaje isti
        public async Task<MessageDto?> SaveMessageAsync(string senderUserId, int conversationId, string content, bool isPrivate)
        {
            // ... (postojeći kod, samo pazi na ime DbContexta ako si ga mijenjao)
            _logger.LogInformation("Saving message from User {SenderUserId} to Conversation {ConversationId}. IsPrivate: {IsPrivate}",
               senderUserId, conversationId, isPrivate);

            if (!await CanUserAccessConversationAsync(senderUserId, conversationId))
            {
                _logger.LogWarning("User {SenderUserId} attempted to send message to Conversation {ConversationId} without access.", senderUserId, conversationId);
                throw new UnauthorizedAccessException("User does not have access to this conversation.");
            }

            var message = new Message // Koristi Conversation.Models.Message ako je potrebno
            {
                ConversationId = conversationId,
                SenderUserId = senderUserId,
                Content = content,
                SentAt = DateTime.UtcNow,
                IsPrivate = isPrivate,
                PreviousMessageId = 0 // Moraš postaviti default vrijednost ako je int (ne nullable)
                                      // ili učiniti PreviousMessageId nullable (int?) u modelu Message.cs
            };

            _context.Messages.Add(message);

            var conversation = await _context.Conversations.FindAsync(conversationId);
            if (conversation != null)
            {
                await _context.SaveChangesAsync();
                conversation.LastMessageId = message.Id;
                _context.Entry(conversation).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Message ID {MessageId} saved. Conversation {ConversationId} LastMessageId updated.", message.Id, conversationId);
            }
            else
            {
                _logger.LogError("Failed to save message. Conversation {ConversationId} not found when trying to update LastMessageId.", conversationId);
                await _context.SaveChangesAsync(); // Ipak sačuvaj poruku
            }

            var senderUser = await _userManager.FindByIdAsync(senderUserId);
            return new MessageDto
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                SenderUserId = message.SenderUserId,
                SenderUsername = senderUser?.UserName,
                Content = message.Content,
                SentAt = message.SentAt,
                ReadAt = message.ReadAt,
                IsPrivate = message.IsPrivate
            };
        }

        public async Task<IEnumerable<ConversationDto>> GetConversationsForUserAsync(string userId)
        {
            _logger.LogInformation("Fetching conversations for User {UserId}", userId);
            var conversations = await _context.Conversations
                .Where(c => c.BuyerUserId == userId || c.SellerUserId == userId)
                // ➤➤➤ ISPRAVKA SORTIRANJA: Dohvati SentAt od zadnje poruke ako postoji LastMessageId ➤➤➤
                .OrderByDescending(c => c.LastMessageId.HasValue ?
                    _context.Messages.Where(m => m.Id == c.LastMessageId).Select(m => m.SentAt).FirstOrDefault() :
                    c.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            var dtos = new List<ConversationDto>();
            foreach (var conv in conversations)
            {
                var dto = await MapConversationToDtoAsync(conv, userId); // Proslijedi Conversation.Models.Conversation
                if (dto != null) dtos.Add(dto);
            }
            return dtos;
        }

        // GetConversationMessagesAsync ostaje isti
        public async Task<IEnumerable<MessageDto>> GetConversationMessagesAsync(int conversationId, string requestingUserId, bool isAdmin, int page = 1, int pageSize = 30)
        {
            // ... (postojeći kod)
            _logger.LogInformation("Fetching messages for Conversation {ConversationId}, User {RequestingUserId}, Page {Page}, Size {PageSize}",
              conversationId, requestingUserId, page, pageSize);

            if (!isAdmin && !await CanUserAccessConversationAsync(requestingUserId, conversationId))
            {
                _logger.LogWarning("User {RequestingUserId} attempted to get messages for Conversation {ConversationId} without access.", requestingUserId, conversationId);
                throw new UnauthorizedAccessException("User does not have access to this conversation.");
            }

            var query = _context.Messages
                .Where(m => m.ConversationId == conversationId)
                .Where(m => isAdmin ? !m.IsPrivate : (m.IsPrivate ? m.SenderUserId == requestingUserId : true))
                .OrderByDescending(m => m.SentAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking();

            var messages = await query.ToListAsync();
            _logger.LogInformation("Retrieved {MessageCount} messages for Conversation {ConversationId}", messages.Count, conversationId);

            var senderIds = messages.Select(m => m.SenderUserId).Distinct().ToList();
            var senders = await _userManager.Users
                                    .Where(u => senderIds.Contains(u.Id))
                                    .ToDictionaryAsync(u => u.Id, u => u.UserName);

            return messages.Select(m => new MessageDto
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                SenderUserId = m.SenderUserId,
                SenderUsername = senders.TryGetValue(m.SenderUserId, out var username) ? username : "Unknown",
                Content = m.Content,
                SentAt = m.SentAt,
                ReadAt = m.ReadAt,
                IsPrivate = m.IsPrivate
            }).Reverse();
        }

        // CanUserAccessConversationAsync ostaje isti
        public async Task<bool> CanUserAccessConversationAsync(string userId, int conversationId)
        {
            // ... (postojeći kod)
            return await _context.Conversations
               .AnyAsync(c => c.Id == conversationId && (c.BuyerUserId == userId || c.SellerUserId == userId));
        }

        // MarkMessagesAsReadAsync ostaje isti
        public async Task<bool> MarkMessagesAsReadAsync(int conversationId, string readerUserId)
        {
            // ... (postojeći kod)
            _logger.LogInformation("Marking messages as read in Conversation {ConversationId} for User {ReaderUserId}",
               conversationId, readerUserId);

            if (!await CanUserAccessConversationAsync(readerUserId, conversationId))
            {
                _logger.LogWarning("User {ReaderUserId} attempted to mark messages as read for Conversation {ConversationId} without access.", readerUserId, conversationId);
                throw new UnauthorizedAccessException("User does not have access to this conversation.");
            }

            var now = DateTime.UtcNow;
            int updatedCount = await _context.Messages
                .Where(m => m.ConversationId == conversationId &&
                             m.SenderUserId != readerUserId &&
                             m.ReadAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.ReadAt, now));

            _logger.LogInformation("Marked {UpdatedCount} messages as read in Conversation {ConversationId} for User {ReaderUserId}",
                updatedCount, conversationId, readerUserId);

            return updatedCount > 0;
        }


        // Pomoćna metoda za mapiranje Conversation u DTO
        // ➤➤➤ ISPRAVLJENO: Koristi LastMessageId za dohvat zadnje poruke ➤➤➤
        private async Task<ConversationDto?> MapConversationToDtoAsync(Conversation.Models.Conversation conv, string currentUserId) // Ekspliciraj tip
        {
            if (conv == null) return null;

            string otherParticipantId = conv.BuyerUserId == currentUserId ? conv.SellerUserId : conv.BuyerUserId;
            var otherParticipant = await _userManager.FindByIdAsync(otherParticipantId);
            var store = await _storeService.GetStoreByIdAsync(conv.StoreId);

            string? productName = null;
            if (conv.ProductId.HasValue)
            {
                var product = await _catalogContext.Products
                                      .AsNoTracking()
                                      .FirstOrDefaultAsync(p => p.Id == conv.ProductId.Value);
                productName = product?.Name;
            }

            MessageDto? lastMessageDto = null;
            if (conv.LastMessageId.HasValue) // Provjeri da li ID postoji
            {
                // Dohvati poruku iz baze koristeći LastMessageId
                var lastMsg = await _context.Messages.FindAsync(conv.LastMessageId.Value);
                if (lastMsg != null)
                {
                    // Provjeri vidljivost poruke
                    bool canCurrentUserSeeLastMessage = !lastMsg.IsPrivate || lastMsg.SenderUserId == currentUserId ||
                                                        (conv.BuyerUserId == currentUserId || conv.SellerUserId == currentUserId);

                    if (canCurrentUserSeeLastMessage)
                    {
                        var lastMsgSender = await _userManager.FindByIdAsync(lastMsg.SenderUserId);
                        lastMessageDto = new MessageDto
                        {
                            Id = lastMsg.Id,
                            ConversationId = lastMsg.ConversationId,
                            SenderUserId = lastMsg.SenderUserId,
                            SenderUsername = lastMsgSender?.UserName,
                            Content = lastMsg.Content,
                            SentAt = lastMsg.SentAt,
                            ReadAt = lastMsg.ReadAt,
                            IsPrivate = lastMsg.IsPrivate
                        };
                    }
                }
            }

            int unreadCount = await _context.Messages
                .CountAsync(m => m.ConversationId == conv.Id &&
                                  m.SenderUserId != currentUserId &&
                                  m.ReadAt == null);

            return new ConversationDto
            {
                Id = conv.Id,
                BuyerUserId = conv.BuyerUserId,
                BuyerUsername = conv.BuyerUserId == currentUserId ? (await _userManager.FindByIdAsync(currentUserId))?.UserName : otherParticipant?.UserName,
                SellerUserId = conv.SellerUserId,
                SellerUsername = conv.SellerUserId == currentUserId ? (await _userManager.FindByIdAsync(currentUserId))?.UserName : otherParticipant?.UserName,
                StoreId = conv.StoreId,
                OrderId = conv.OrderId,
                ProductId = conv.ProductId,
                ProductName = productName,
                CreatedAt = conv.CreatedAt,
                LastMessage = lastMessageDto, // Koristi dohvaćeni DTO
                UnreadMessagesCount = unreadCount
            };
        }
    }
}