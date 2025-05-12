using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Catalog.Models;
using Catalog.Models;
using Chat.Dtos;
using Chat.Interfaces;
using Conversation.Data;
using Conversation.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notifications.Interfaces;
using Store.Interface;
using Users.Models;

namespace Chat.Services
{
    public class ChatService : IChatService
    {
        private readonly ConversationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IStoreService _storeService;
        private readonly CatalogDbContext _catalogContext;
        private readonly ILogger<ChatService> _logger;
        private readonly INotificationService _dbNotificationService;
        private readonly IPushNotificationService _pushNotificationService;

        public ChatService(
            ConversationDbContext context,
            UserManager<User> userManager,
            IStoreService storeService,
            CatalogDbContext catalogContext,
            INotificationService dbNotificationService,
            IPushNotificationService pushNotificationService,
            ILogger<ChatService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _storeService = storeService ?? throw new ArgumentNullException(nameof(storeService));
            _catalogContext = catalogContext ?? throw new ArgumentNullException(nameof(catalogContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dbNotificationService = dbNotificationService ?? throw new ArgumentNullException(nameof(dbNotificationService));
            _pushNotificationService = pushNotificationService ?? throw new ArgumentNullException(nameof(pushNotificationService));
        }

        public async Task<ConversationDto?> GetOrCreateConversationAsync(
            string requestingUserId, string targetUserId, int storeId, int? orderId = null, int? productId = null)
        {
            _logger.LogInformation(
                "GetOrCreateConversation: RequestingUser: {RU}, TargetUser: {TU}, Store: {S}, Order: {O}, Product: {P}",
                requestingUserId, targetUserId, storeId, orderId?.ToString() ?? "N/A", productId?.ToString() ?? "N/A");

            // Validacija: Ne mogu i OrderId i ProductId biti postavljeni istovremeno
            if (orderId.HasValue && productId.HasValue)
            {
                _logger.LogWarning("GetOrCreateConversation failed: Both OrderId and ProductId were provided.");
                throw new ArgumentException("A conversation can be related to an Order or a Product, but not both simultaneously.");
            }

            var requestingUserObj = await _userManager.FindByIdAsync(requestingUserId);
            var targetUserObj = await _userManager.FindByIdAsync(targetUserId);

            if (requestingUserObj == null) throw new KeyNotFoundException($"Requesting user {requestingUserId} not found.");
            if (targetUserObj == null) throw new KeyNotFoundException($"Target user {targetUserId} not found.");

            string buyerId, sellerId;

            // Određivanje Buyer-a i Seller-a
            // Pretpostavka: Seller je korisnik čiji StoreId odgovara proslijeđenom storeId
            // Ako oba korisnika imaju isti StoreId, a nije isti korisnik, to je greška u logici ili podacima
            // Ako nijedan nema taj StoreId, to je takođe greška.
            bool isRequestingUserSellerForStore = requestingUserObj.StoreId.HasValue && requestingUserObj.StoreId.Value == storeId;
            bool isTargetUserSellerForStore = targetUserObj.StoreId.HasValue && targetUserObj.StoreId.Value == storeId;

            if (isTargetUserSellerForStore && !isRequestingUserSellerForStore)
            {
                // Target je Seller, Requesting je Buyer
                sellerId = targetUserObj.Id;
                buyerId = requestingUserObj.Id;
                if (buyerId == sellerId) // Buyer ne može biti isti kao Seller
                    throw new InvalidOperationException("Buyer and Seller cannot be the same user for the store.");
            }
            else if (isRequestingUserSellerForStore && !isTargetUserSellerForStore)
            {
                // Requesting je Seller, Target je Buyer
                sellerId = requestingUserObj.Id;
                buyerId = targetUserObj.Id;
                if (buyerId == sellerId)
                    throw new InvalidOperationException("Buyer and Seller cannot be the same user for the store.");
            }
            else if (isRequestingUserSellerForStore && isTargetUserSellerForStore && requestingUserObj.Id != targetUserObj.Id)
            {
                // Oba su Selleri za istu prodavnicu - ovo nema smisla za Buyer-Seller chat
                _logger.LogError("Ambiguous situation: Both users {User1} and {User2} are sellers for Store {StoreId}.", requestingUserId, targetUserId, storeId);
                throw new InvalidOperationException("Cannot determine a clear buyer-seller relationship for this store when both are sellers.");
            }
            else if (requestingUserObj.Id == targetUserObj.Id) // Korisnik pokušava chatati sam sa sobom
            {
                throw new InvalidOperationException("User cannot create a conversation with themselves.");
            }
            else // Nijedan nije jasno Seller za taj store, ili neka druga nepredviđena situacija
            {
                _logger.LogError("Could not determine definitive Buyer/Seller roles for conversation between {User1} and {User2} regarding Store {StoreId}.", requestingUserId, targetUserId, storeId);
                throw new InvalidOperationException("Could not establish a valid buyer-seller relationship for this store.");
            }


            // Izgradnja upita za pronalaženje postojeće konverzacije
            var query = _context.Conversations.AsQueryable();

            // Buyer i Seller mogu biti zamijenjeni, pa provjeravamo obje kombinacije
            query = query.Where(c =>
                ((c.BuyerUserId == buyerId && c.SellerUserId == sellerId) || (c.BuyerUserId == sellerId && c.SellerUserId == buyerId)) &&
                c.StoreId == storeId);

            if (orderId.HasValue)
            {
                query = query.Where(c => c.OrderId == orderId.Value && c.ProductId == null);
            }
            else if (productId.HasValue)
            {
                query = query.Where(c => c.ProductId == productId.Value && c.OrderId == null);
            }
            else // Generalna konverzacija za prodavnicu
            {
                query = query.Where(c => c.OrderId == null && c.ProductId == null);
            }

            var existingConversation = await query.FirstOrDefaultAsync();

            if (existingConversation != null)
            {
                _logger.LogInformation("Found existing conversation ID: {ConversationId}", existingConversation.Id);
                return await MapConversationToDtoAsync(existingConversation, requestingUserId);
            }

            _logger.LogInformation("Creating new conversation. Buyer: {B}, Seller: {S}, Store: {Store}, Order: {O}, Product: {P}",
                buyerId, sellerId, storeId, orderId?.ToString() ?? "N/A", productId?.ToString() ?? "N/A");

            var newConversation = new Conversation.Models.Conversation
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

        public async Task<MessageDto?> SaveMessageAsync(string senderUserId, int conversationId, string content, bool isPrivate)
        {
            _logger.LogInformation("Attempting to save message from User {SenderUserId} to Conversation {ConversationId}. IsPrivate: {IsPrivate}. Content snippet: '{ContentSnippet}'",
                senderUserId, conversationId, isPrivate, content.Substring(0, Math.Min(30, content.Length)));

            // 1. Provjeri da li korisnik pripada konverzaciji
            var conversation = await _context.Conversations.FindAsync(conversationId);
            if (conversation == null)
            {
                _logger.LogWarning("SaveMessage failed: Conversation {ConversationId} not found.", conversationId);
                throw new KeyNotFoundException($"Conversation with ID {conversationId} not found.");
            }
            if (conversation.BuyerUserId != senderUserId && conversation.SellerUserId != senderUserId)
            {
                _logger.LogWarning("User {SenderUserId} attempted to send message to Conversation {ConversationId} they do not belong to.", senderUserId, conversationId);
                throw new UnauthorizedAccessException("User does not have access to send messages in this conversation.");
            }

            // 2. Kreiraj i sačuvaj poruku
            var message = new Conversation.Models.Message
            {
                ConversationId = conversationId,
                SenderUserId = senderUserId,
                Content = content,
                SentAt = DateTime.UtcNow,
                IsPrivate = isPrivate,
                PreviousMessageId = 0
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            conversation.LastMessageId = message.Id;
            _context.Entry(conversation).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Message ID {MessageId} saved. Conversation {ConversationId} LastMessageId updated to {LastMessageId}.", message.Id, conversationId, message.Id);

            try
            {
                string recipientUserId = conversation.BuyerUserId == senderUserId ? conversation.SellerUserId : conversation.BuyerUserId;
                var recipientUser = await _userManager.FindByIdAsync(recipientUserId);
                var senderUser = await _userManager.FindByIdAsync(senderUserId);

                if (recipientUser != null)
                {
                    string notificationMessage = $"Nova poruka od {senderUser?.UserName ?? "korisnika"}: {content.Substring(0, Math.Min(50, content.Length))}{(content.Length > 50 ? "..." : "")}";
                    string pushTitle = $"Nova poruka od {senderUser?.UserName ?? "Korisnika"}";

                    await _dbNotificationService.CreateNotificationAsync(
                        recipientUserId,
                        notificationMessage,
                        conversationId
                    );
                    _logger.LogInformation("DB Notification creation task initiated for Recipient {RecipientUserId} for new message in Conversation {ConversationId}.", recipientUserId, conversationId);

                    if (!string.IsNullOrWhiteSpace(recipientUser.FcmDeviceToken))
                    {
                        var pushData = new Dictionary<string, string> {
                                { "conversationId", conversationId.ToString() },
                                { "messageId", message.Id.ToString() },
                                { "screen", "ChatScreen" }
                            };
                        try
                        {
                            await _pushNotificationService.SendPushNotificationAsync(
                                recipientUser.FcmDeviceToken,
                                pushTitle,
                                content,
                                pushData
                            );
                            _logger.LogInformation("Push Notification task initiated for Recipient {RecipientUserId} for new message.", recipientUserId);
                        }
                        catch (Exception pushEx)
                        {
                            _logger.LogError(pushEx, "Failed to send Push Notification to Recipient {RecipientUserId} for message in Conversation {ConversationId}.", recipientUserId, conversationId);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Recipient {RecipientUserId} has no FcmDeviceToken for push notification.", recipientUserId);
                    }
                }
                else
                {
                    _logger.LogWarning("Could not find recipient user {RecipientUserId} for notification.", recipientUserId);
                }
            }
            catch (Exception notifyEx)
            {
                _logger.LogError(notifyEx, "Error sending notification for new message from {SenderUserId} in Conversation {ConversationId}.", senderUserId, conversationId);
            }

            // 4. Mapiraj i vrati DTO sačuvane poruke
            return new MessageDto
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                SenderUserId = message.SenderUserId,
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
                .OrderByDescending(c => c.LastMessageId.HasValue ?
                    _context.Messages.Where(m => m.Id == c.LastMessageId).Select(m => m.SentAt).FirstOrDefault() :
                    c.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            var dtos = new List<ConversationDto>();
            foreach (var conv in conversations)
            {
                var dto = await MapConversationToDtoAsync(conv, userId);
                if (dto != null) dtos.Add(dto);
            }
            return dtos;
        }

        public async Task<IEnumerable<MessageDto>> GetConversationMessagesAsync(int conversationId, string requestingUserId, bool isAdmin, int page = 1, int pageSize = 30)
        {
            _logger.LogInformation("Fetching messages for Conversation {ConversationId}, User {RequestingUserId} (IsAdmin: {IsAdmin}), Page {Page}, Size {PageSize}",
               conversationId, requestingUserId, isAdmin, page, pageSize);

            if (!isAdmin && !await CanUserAccessConversationAsync(requestingUserId, conversationId))
            {
                _logger.LogWarning("User {RequestingUserId} attempted to get messages for Conversation {ConversationId} without access.", requestingUserId, conversationId);
                throw new UnauthorizedAccessException("User does not have access to this conversation.");
            }

            var conversationExists = await _context.Conversations.AnyAsync(c => c.Id == conversationId);
            if (!conversationExists)
            {
                _logger.LogWarning("Conversation {ConversationId} not found when fetching messages.", conversationId);
                return Enumerable.Empty<MessageDto>();
            }


            var query = _context.Messages
                .Where(m => m.ConversationId == conversationId)
                .Where(m => !m.IsPrivate || (m.IsPrivate && m.SenderUserId == requestingUserId) || (isAdmin && !m.IsPrivate))
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
                SenderUsername = senders.TryGetValue(m.SenderUserId, out var username) ? username : "Nepoznat",
                Content = m.IsPrivate && isAdmin ? "[Privatna poruka - sakriveno za admina]" : m.Content,
                SentAt = m.SentAt,
                ReadAt = m.ReadAt,
                IsPrivate = m.IsPrivate
            }).Reverse();
        }

        public async Task<bool> CanUserAccessConversationAsync(string userId, int conversationId)
        {
            return await _context.Conversations
                .AnyAsync(c => c.Id == conversationId && (c.BuyerUserId == userId || c.SellerUserId == userId));
        }

        public async Task<bool> MarkMessagesAsReadAsync(int conversationId, string readerUserId)
        {
            _logger.LogInformation("Marking messages as read in Conversation {ConversationId} for User {ReaderUserId}",
                conversationId, readerUserId);

            if (!await CanUserAccessConversationAsync(readerUserId, conversationId))
            {
                _logger.LogWarning("User {ReaderUserId} attempted to mark messages as read for Conversation {ConversationId} without access.", readerUserId, conversationId);
                throw new UnauthorizedAccessException("User does not have access to this conversation.");
            }

            var now = DateTime.UtcNow;
            // Označi kao pročitane sve poruke u konverzaciji koje NIJE poslao trenutni korisnik i koje još nisu pročitane
            int updatedCount = await _context.Messages
                .Where(m => m.ConversationId == conversationId &&
                             m.SenderUserId != readerUserId &&
                             m.ReadAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.ReadAt, now)); // EF Core 7+

            _logger.LogInformation("Marked {UpdatedCount} messages as read in Conversation {ConversationId} for User {ReaderUserId}",
                updatedCount, conversationId, readerUserId);

            // Vrati true ako je bar jedna poruka ažurirana (ili uvijek true ako operacija prođe?)
            return updatedCount >= 0; // Vraća true čak i ako nijedna nije ažurirana (jer nije bilo nepročitanih)
        }


        private async Task<ConversationDto?> MapConversationToDtoAsync(Conversation.Models.Conversation conv, string currentUserId)
        {
            if (conv == null) return null;

            string otherParticipantId = conv.BuyerUserId == currentUserId ? conv.SellerUserId : conv.BuyerUserId;
            var otherParticipant = await _userManager.FindByIdAsync(otherParticipantId);
            var currentUser = await _userManager.FindByIdAsync(currentUserId); // Treba nam i trenutni korisnik
            var store = _storeService.GetStoreById(conv.StoreId);

            string? productName = null;
            if (conv.ProductId.HasValue)
            {
                var product = await _catalogContext.Products
                                      .AsNoTracking()
                                      .FirstOrDefaultAsync(p => p.Id == conv.ProductId.Value);
                productName = product?.Name;
            }

            MessageDto? lastMessageDto = null;
            if (conv.LastMessageId.HasValue)
            {
                var lastMsg = await _context.Messages.FindAsync(conv.LastMessageId.Value);
                if (lastMsg != null)
                {
                    // Provjeri da li trenutni korisnik (ili admin koji nije učesnik) smije vidjeti ovu poruku
                    bool isAdminViewing = await _userManager.IsInRoleAsync(currentUser, "Admin") &&
                                         !(currentUser.Id == conv.BuyerUserId || currentUser.Id == conv.SellerUserId);

                    bool canCurrentUserSeeLastMessage = (!lastMsg.IsPrivate) || // Ako nije privatna, svi je vide
                                                       (lastMsg.IsPrivate && (lastMsg.SenderUserId == currentUserId || !isAdminViewing)); // Ako je privatna, vidi je pošiljalac, ili drugi učesnik (ako nije admin koji gleda tuđe)

                    if (canCurrentUserSeeLastMessage)
                    {
                        var lastMsgSender = await _userManager.FindByIdAsync(lastMsg.SenderUserId);
                        lastMessageDto = new MessageDto
                        {
                            Id = lastMsg.Id,
                            ConversationId = lastMsg.ConversationId,
                            SenderUserId = lastMsg.SenderUserId,
                            SenderUsername = lastMsgSender?.UserName,
                            Content = lastMsg.IsPrivate && isAdminViewing ? "[Privatna poruka]" : lastMsg.Content, // Admin ne vidi sadržaj privatnih
                            SentAt = lastMsg.SentAt,
                            ReadAt = lastMsg.ReadAt,
                            IsPrivate = lastMsg.IsPrivate
                        };
                    }
                }
            }

            int unreadCount = await _context.Messages
                .CountAsync(m => m.ConversationId == conv.Id &&
                                  m.SenderUserId != currentUserId && // Nije poslao trenutni korisnik
                                  m.ReadAt == null); // I nije pročitana

            return new ConversationDto
            {
                Id = conv.Id,
                BuyerUserId = conv.BuyerUserId,
                BuyerUsername = (conv.BuyerUserId == currentUserId ? currentUser?.UserName : otherParticipant?.UserName) ?? "Nepoznat",
                SellerUserId = conv.SellerUserId,
                SellerUsername = (conv.SellerUserId == currentUserId ? currentUser?.UserName : otherParticipant?.UserName) ?? "Nepoznat",
                StoreId = conv.StoreId,
                OrderId = conv.OrderId,
                ProductId = conv.ProductId,
                ProductName = productName,
                CreatedAt = conv.CreatedAt,
                LastMessage = lastMessageDto,
                UnreadMessagesCount = unreadCount
            };
        }

        public async Task<IEnumerable<MessageDto>> GetAllMessagesForConversationAsync(int conversationId, string requestingUserId, bool isAdmin, int page = 1, int pageSize = 30)
        {
            _logger.LogInformation("[GetAllMessages] User {UserId} (IsAdmin: {IsAdmin}) fetching ALL messages for Conversation {ConversationId}, Page: {Page}, Size: {PageSize}",
                requestingUserId, isAdmin, conversationId, page, pageSize);

            // 1. Autorizacija: Provjeri da li korisnik uopšte ima pristup ovoj konverzaciji
            bool isParticipant = await CanUserAccessConversationAsync(requestingUserId, conversationId);
            if (!isAdmin && !isParticipant)
            {
                _logger.LogWarning("[GetAllMessages] User {RequestingUserId} unauthorized access to Conversation {ConversationId}", requestingUserId, conversationId);
                throw new UnauthorizedAccessException("User does not have access to this conversation.");
            }

            var conversationExists = await _context.Conversations.AnyAsync(c => c.Id == conversationId);
            if (!conversationExists)
            {
                _logger.LogWarning("[GetAllMessages] Conversation {ConversationId} not found.", conversationId);
                return Enumerable.Empty<MessageDto>();
            }

            // 2. Dohvati SVE poruke za dati conversationId
            var query = _context.Messages
                .Where(m => m.ConversationId == conversationId);

            // 3. Primijeni filter za privatne poruke na osnovu toga da li je Admin i da li je učesnik
            if (isAdmin && !isParticipant)
            {
                query = query.Where(m => !m.IsPrivate);
                _logger.LogDebug("[GetAllMessages] Admin {AdminUserId} viewing non-private messages for Conversation {ConversationId}", requestingUserId, conversationId);
            }


            query = query.OrderByDescending(m => m.SentAt) // Najnovije prvo za paginaciju
                         .Skip((page - 1) * pageSize)
                         .Take(pageSize)
                         .AsNoTracking();

            var messages = await query.ToListAsync();
            _logger.LogInformation("[GetAllMessages] Retrieved {MessageCount} messages for Conversation {ConversationId}", messages.Count, conversationId);

            // 4. Dohvati imena pošiljaoca
            var senderIds = messages.Select(m => m.SenderUserId).Distinct().ToList();
            var senders = new Dictionary<string, string?>();
            if (senderIds.Any())
            {
                senders = await _userManager.Users
                                    .Where(u => senderIds.Contains(u.Id))
                                    .ToDictionaryAsync(u => u.Id, u => u.UserName);
            }

            // 5. Mapiraj u DTO
            return messages.Select(m => new MessageDto
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                SenderUserId = m.SenderUserId,
                SenderUsername = senders.TryGetValue(m.SenderUserId, out var username) ? username : "Nepoznat",
                Content = (m.IsPrivate && isAdmin && !isParticipant) ? "[Privatna poruka - sakriveno za admina]" : m.Content,
                SentAt = m.SentAt,
                ReadAt = m.ReadAt,
                IsPrivate = m.IsPrivate
            }).Reverse();
        }
    }
}