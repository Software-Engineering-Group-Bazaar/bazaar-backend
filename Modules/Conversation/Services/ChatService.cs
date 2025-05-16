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

        public async Task<ConversationDto?> GetOrCreateConversationAsync(string requestingUserId, string targetUserId, int storeId, int? orderId = null, int? productId = null, int? ticketId = null)
        {
            _logger.LogInformation(
                "GetOrCreateConversation - Input: Requesting: {RU}, Target: {TU}, Store: {S}, Order: {O}, Product: {P}, Ticket: {T}",
                requestingUserId, targetUserId, storeId, orderId?.ToString() ?? "N/A", productId?.ToString() ?? "N/A", ticketId?.ToString() ?? "N/A");

            int? finalOrderId = (orderId.HasValue && orderId.Value == 0) ? null : orderId;
            int? finalProductId = (productId.HasValue && productId.Value == 0) ? null : productId;

            var requestingUserObj = await _userManager.FindByIdAsync(requestingUserId);
            if (requestingUserObj == null) throw new KeyNotFoundException($"Requesting user {requestingUserId} not found.");

            if (ticketId.HasValue)
            {
                var adminUserObj = await _userManager.FindByIdAsync(targetUserId);
                if (adminUserObj == null) throw new KeyNotFoundException($"Admin user {targetUserId} not found for ticket conversation.");
                if (!(await _userManager.IsInRoleAsync(adminUserObj, "Admin")))
                    throw new InvalidOperationException($"User {targetUserId} is not an Admin.");

                if (requestingUserObj == null) throw new KeyNotFoundException($"Requesting user {requestingUserId} (ticket creator) not found.");


                _logger.LogInformation("Processing as a TICKET-based conversation for TicketId: {TicketId} between User: {UserId} and Admin: {AdminId}",
                    ticketId.Value, requestingUserId, targetUserId);

                var existingConversation = await _context.Conversations
                    .FirstOrDefaultAsync(c => c.TicketId == ticketId.Value &&
                                             ((c.BuyerUserId == requestingUserId && c.AdminUserId == targetUserId) ||
                                              (c.SellerUserId == requestingUserId && c.AdminUserId == targetUserId)));

                if (existingConversation != null)
                {
                    _logger.LogInformation("Found existing TICKET conversation ID: {ConversationId}", existingConversation.Id);
                    return await MapConversationToDtoAsync(existingConversation, requestingUserId);
                }

                _logger.LogInformation("Creating new TICKET conversation. User: {UserId}, Admin: {AdminId}, Ticket: {TicketId}",
                    requestingUserId, targetUserId, ticketId.Value);

                string? conversationBuyerId = null;
                string? conversationSellerId = null;

                if (requestingUserObj.StoreId.HasValue && requestingUserObj.StoreId.Value == storeId)
                {
                    conversationSellerId = requestingUserObj.Id;
                    conversationBuyerId = null;
                }
                else
                {
                    conversationBuyerId = requestingUserObj.Id;
                    conversationSellerId = null;
                }

                var newConversation = new Conversation.Models.Conversation
                {
                    BuyerUserId = conversationBuyerId,
                    SellerUserId = conversationSellerId,
                    AdminUserId = targetUserId,
                    StoreId = storeId,
                    OrderId = finalOrderId,
                    ProductId = finalProductId,
                    TicketId = ticketId.Value,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Conversations.Add(newConversation);
                await _context.SaveChangesAsync();
                _logger.LogInformation("New TICKET conversation created with ID: {ConversationId}", newConversation.Id);
                return await MapConversationToDtoAsync(newConversation, requestingUserId);
            }
            else
            {
                _logger.LogInformation("Processing as a BUYER-SELLER conversation.");
                if (string.IsNullOrEmpty(targetUserId))
                {
                    throw new ArgumentException("TargetUserId is required for Buyer-Seller conversations.");
                }

                var targetUserObjForBS = await _userManager.FindByIdAsync(targetUserId);
                if (targetUserObjForBS == null) throw new KeyNotFoundException($"Target user {targetUserId} not found for Buyer-Seller chat.");

                string buyerId, sellerId;
                bool isRequestingUserSellerForStore = requestingUserObj.StoreId.HasValue && requestingUserObj.StoreId.Value == storeId;
                bool isTargetUserSellerForStore = targetUserObjForBS.StoreId.HasValue && targetUserObjForBS.StoreId.Value == storeId;

                if (isTargetUserSellerForStore && !isRequestingUserSellerForStore)
                {
                    sellerId = targetUserObjForBS.Id;
                    buyerId = requestingUserObj.Id;
                    if (buyerId == sellerId) throw new InvalidOperationException("Buyer and Seller cannot be the same user.");
                }
                else if (isRequestingUserSellerForStore && !isTargetUserSellerForStore)
                {
                    sellerId = requestingUserObj.Id;
                    buyerId = targetUserObjForBS.Id;
                    if (buyerId == sellerId) throw new InvalidOperationException("Buyer and Seller cannot be the same user.");
                }
                else if (isRequestingUserSellerForStore && isTargetUserSellerForStore && requestingUserObj.Id != targetUserObjForBS.Id)
                {
                    throw new InvalidOperationException("Ambiguous: Both users are sellers for the same store.");
                }
                else if (requestingUserObj.Id == targetUserObjForBS.Id)
                {
                    throw new InvalidOperationException("User cannot create a conversation with themselves.");
                }
                else
                {
                    throw new InvalidOperationException("Could not establish a valid buyer-seller relationship for this store.");
                }

                var query = _context.Conversations.AsQueryable();
                query = query.Where(c =>
                    ((c.BuyerUserId == buyerId && c.SellerUserId == sellerId) || (c.BuyerUserId == sellerId && c.SellerUserId == buyerId)) &&
                    c.StoreId == storeId && c.TicketId == null);

                if (finalOrderId.HasValue) query = query.Where(c => c.OrderId == finalOrderId.Value && c.ProductId == null);
                else if (finalProductId.HasValue) query = query.Where(c => c.ProductId == finalProductId.Value && c.OrderId == null);
                else query = query.Where(c => c.OrderId == null && c.ProductId == null);

                var existingConversation = await query.FirstOrDefaultAsync();

                if (existingConversation != null)
                {
                    _logger.LogInformation("Found existing BUYER-SELLER conversation ID: {ConversationId}", existingConversation.Id);
                    return await MapConversationToDtoAsync(existingConversation, requestingUserId);
                }

                _logger.LogInformation("Creating new BUYER-SELLER conversation. Buyer: {B}, Seller: {S}, Store: {Store}, Order: {O}, Product: {P}",
                    buyerId, sellerId, storeId, finalOrderId?.ToString() ?? "N/A", finalProductId?.ToString() ?? "N/A");

                var newConversation = new Conversation.Models.Conversation
                {
                    BuyerUserId = buyerId,
                    SellerUserId = sellerId,
                    StoreId = storeId,
                    OrderId = finalOrderId,
                    ProductId = finalProductId,
                    AdminUserId = null,
                    TicketId = null,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Conversations.Add(newConversation);
                await _context.SaveChangesAsync();
                _logger.LogInformation("New BUYER-SELLER conversation created with ID: {ConversationId}", newConversation.Id);
                return await MapConversationToDtoAsync(newConversation, requestingUserId);
            }
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
            if (conversation.BuyerUserId != senderUserId && conversation.SellerUserId != senderUserId && conversation.AdminUserId != senderUserId)
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
                string? recipientUserId = null;

                if (conversation.TicketId.HasValue && !string.IsNullOrEmpty(conversation.AdminUserId))
                {
                    if (conversation.AdminUserId == senderUserId)
                    {
                        recipientUserId = !string.IsNullOrEmpty(conversation.BuyerUserId) ? conversation.BuyerUserId : conversation.SellerUserId;
                    }
                    else if (conversation.BuyerUserId == senderUserId || conversation.SellerUserId == senderUserId)
                    {
                        recipientUserId = conversation.AdminUserId;
                    }
                }
                else if (!string.IsNullOrEmpty(conversation.BuyerUserId) && !string.IsNullOrEmpty(conversation.SellerUserId))
                {
                    recipientUserId = conversation.BuyerUserId == senderUserId ? conversation.SellerUserId : conversation.BuyerUserId;
                }

                if (string.IsNullOrEmpty(recipientUserId))
                {
                    _logger.LogWarning("Could not determine recipient for notification in Conversation {ConversationId}. Sender: {SenderUserId}, Buyer: {BuyerId}, Seller: {SellerId}, Admin: {AdminId}",
                        conversationId, senderUserId, conversation.BuyerUserId, conversation.SellerUserId, conversation.AdminUserId);
                }
                else
                {
                    var recipientUser = await _userManager.FindByIdAsync(recipientUserId);
                    var senderUser = await _userManager.FindByIdAsync(senderUserId);

                    if (recipientUser != null && senderUser != null)
                    {
                        string notificationMessage = $"Nova poruka od {senderUser.UserName ?? "korisnika"}: {content.Substring(0, Math.Min(50, content.Length))}{(content.Length > 50 ? "..." : "")}";
                        string pushTitle = $"Nova poruka od {senderUser.UserName ?? "Korisnika"}";

                        // DB Notifikacija
                        await _dbNotificationService.CreateNotificationAsync(
                            recipientUserId,
                            notificationMessage,
                            conversationId // Poveži sa ConversationId
                        );
                        _logger.LogInformation("DB Notification creation task initiated for Recipient {RecipientUserId} for new message in Conversation {ConversationId}.", recipientUserId, conversationId);

                        // Push Notifikacija
                        if (!string.IsNullOrWhiteSpace(recipientUser.FcmDeviceToken))
                        {
                            var pushData = new Dictionary<string, string> {
                                { "conversationId", conversationId.ToString() },
                                { "messageId", message.Id.ToString() }, // message je sačuvana poruka
                                { "senderId", senderUserId },
                                { "screen", "ChatScreen" }
                            };
                            try
                            {
                                await _pushNotificationService.SendPushNotificationAsync(
                                    recipientUser.FcmDeviceToken,
                                    pushTitle,
                                    content, // Puni sadržaj za push
                                    pushData
                                );
                                _logger.LogInformation("Push Notification task initiated for Recipient {RecipientUserId} (Token: ...{TokenEnd}) for new message.",
                                    recipientUserId, recipientUser.FcmDeviceToken.Length > 5 ? recipientUser.FcmDeviceToken.Substring(recipientUser.FcmDeviceToken.Length - 5) : "short");
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
                        _logger.LogWarning("Could not find recipient user ({RecipientUserId}) or sender user ({SenderUserId}) for notification.", recipientUserId, senderUserId);
                    }
                }
            }
            catch (Exception notifyEx)
            {
                _logger.LogError(notifyEx, "Error sending notification for new message from {SenderUserId} in Conversation {ConversationId}.", senderUserId, conversationId);
            }

            // Mapiraj i vrati DTO sačuvane poruke
            // Treba dohvatiti senderUser ponovo ili ga proslijediti ako ga imamo od gore
            var finalSenderUser = await _userManager.FindByIdAsync(senderUserId);
            return new MessageDto
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                SenderUserId = message.SenderUserId,
                SenderUsername = finalSenderUser?.UserName,
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
                .Where(c => c.BuyerUserId == userId || c.SellerUserId == userId || c.AdminUserId == userId)
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

        public async Task<IEnumerable<MessageDto>> GetConversationMessagesAsync(int conversationId, string requestingUserId, bool isAdminFromController, int page = 1, int pageSize = 30)
        {
            _logger.LogInformation("Fetching messages for Conversation {CID}, User {UID} (IsAdminCtrl: {IsAdmin}), Page {P}, Size {PS}",
               conversationId, requestingUserId, isAdminFromController, page, pageSize);

            var conversation = await _context.Conversations
                                      .AsNoTracking()
                                      .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
            {
                _logger.LogWarning("Conversation {ConversationId} not found when fetching messages.", conversationId);
                return Enumerable.Empty<MessageDto>();
            }

            bool isDirectParticipant = conversation.BuyerUserId == requestingUserId ||
                                       conversation.SellerUserId == requestingUserId ||
                                       conversation.AdminUserId == requestingUserId;

            if (!isAdminFromController && !isDirectParticipant)
            {
                _logger.LogWarning("User {RequestingUserId} attempted to get messages for Conversation {ConversationId} without being admin or participant.", requestingUserId, conversationId);
                throw new UnauthorizedAccessException("User does not have access to this conversation.");
            }

            var query = _context.Messages
                .Where(m => m.ConversationId == conversationId);

            if (isAdminFromController && !isDirectParticipant)
            {
                query = query.Where(m => !m.IsPrivate);
                _logger.LogDebug("Admin {AdminUserId} (not participant) viewing non-private messages for Conversation {ConversationId}", requestingUserId, conversationId);
            }

            query = query.OrderByDescending(m => m.SentAt)
                         .Skip((page - 1) * pageSize)
                         .Take(pageSize)
                         .AsNoTracking();

            var messages = await query.ToListAsync();
            _logger.LogInformation("Retrieved {MessageCount} messages for Conversation {ConversationId}", messages.Count, conversationId);

            var senderIds = messages.Select(m => m.SenderUserId).Distinct().ToList();
            var senders = new Dictionary<string, string?>();
            if (senderIds.Any()) { senders = await _userManager.Users.Where(u => senderIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.UserName); }

            return messages.Select(m => new MessageDto
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                SenderUserId = m.SenderUserId,
                SenderUsername = senders.TryGetValue(m.SenderUserId, out var username) ? username : "Nepoznat",
                Content = m.Content,
                SentAt = m.SentAt,
                ReadAt = m.ReadAt,
                IsPrivate = m.IsPrivate
            }).Reverse();
        }

        public async Task<bool> CanUserAccessConversationAsync(string userId, int conversationId)
        {
            return await _context.Conversations
         .AnyAsync(c => c.Id == conversationId &&
                        (c.BuyerUserId == userId ||
                         c.SellerUserId == userId ||
                         c.AdminUserId == userId));
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
            int updatedCount = await _context.Messages
                .Where(m => m.ConversationId == conversationId &&
                             m.SenderUserId != readerUserId &&
                             m.ReadAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.ReadAt, now));

            _logger.LogInformation("Marked {UpdatedCount} messages as read in Conversation {ConversationId} for User {ReaderUserId}",
                updatedCount, conversationId, readerUserId);

            return updatedCount >= 0;
        }


        private async Task<ConversationDto?> MapConversationToDtoAsync(Conversation.Models.Conversation conv, string currentUserId)
        {
            if (conv == null) return null;

            var currentUser = await _userManager.FindByIdAsync(currentUserId);
            if (currentUser == null)
            {
                _logger.LogWarning("MapConversationToDtoAsync: CurrentUser with ID {CurrentUserId} not found.", currentUserId);
                return null;
            }
            bool isCurrentUserAdmin = await _userManager.IsInRoleAsync(currentUser, "Admin");

            User? buyerUserObj = null;
            User? sellerUserObj = null;
            User? adminUserObj = null;
            User? otherParticipantObj = null;

            string? buyerUsername = null;
            string? sellerUsername = null;
            string? adminUsername = null;

            if (!string.IsNullOrEmpty(conv.BuyerUserId))
            {
                buyerUserObj = await _userManager.FindByIdAsync(conv.BuyerUserId);
                buyerUsername = buyerUserObj?.UserName;
            }
            if (!string.IsNullOrEmpty(conv.SellerUserId))
            {
                sellerUserObj = await _userManager.FindByIdAsync(conv.SellerUserId);
                sellerUsername = sellerUserObj?.UserName;
            }
            if (!string.IsNullOrEmpty(conv.AdminUserId))
            {
                adminUserObj = await _userManager.FindByIdAsync(conv.AdminUserId);
                adminUsername = adminUserObj?.UserName;
            }

            if (conv.TicketId.HasValue && !string.IsNullOrEmpty(conv.AdminUserId))
            {
                if (currentUserId == conv.AdminUserId)
                {
                    string? ticketCreatorId = !string.IsNullOrEmpty(conv.BuyerUserId) ? conv.BuyerUserId : conv.SellerUserId;
                    if (!string.IsNullOrEmpty(ticketCreatorId))
                    {
                        otherParticipantObj = (ticketCreatorId == conv.BuyerUserId) ? buyerUserObj : sellerUserObj;
                    }
                }
                else
                {
                    otherParticipantObj = adminUserObj;
                }
            }
            else
            {
                if (conv.BuyerUserId == currentUserId && !string.IsNullOrEmpty(conv.SellerUserId))
                {
                    otherParticipantObj = sellerUserObj;
                }
                else if (conv.SellerUserId == currentUserId && !string.IsNullOrEmpty(conv.BuyerUserId))
                {
                    otherParticipantObj = buyerUserObj;
                }
            }

            string? storeName = null;
            if (conv.StoreId.HasValue)
            {
                var store = _storeService.GetStoreById(conv.StoreId.Value);
                storeName = store?.name;
            }

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
                    bool isCurrentUserAParticipantInConv = (conv.BuyerUserId == currentUserId ||
                                                            conv.SellerUserId == currentUserId ||
                                                            conv.AdminUserId == currentUserId);

                    bool canSeePrivateContent = (lastMsg.IsPrivate &&
                                                (lastMsg.SenderUserId == currentUserId || isCurrentUserAParticipantInConv));

                    bool canSeeMessage = !lastMsg.IsPrivate || canSeePrivateContent;

                    bool hideContentForAdminViewer = lastMsg.IsPrivate && isCurrentUserAdmin && !isCurrentUserAParticipantInConv;


                    if (canSeeMessage)
                    {
                        var lastMsgSender = await _userManager.FindByIdAsync(lastMsg.SenderUserId);
                        lastMessageDto = new MessageDto
                        {
                            Id = lastMsg.Id,
                            ConversationId = lastMsg.ConversationId,
                            SenderUserId = lastMsg.SenderUserId,
                            SenderUsername = lastMsgSender?.UserName,
                            Content = hideContentForAdminViewer ? "[Privatna poruka]" : lastMsg.Content,
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
                                  m.ReadAt == null &&
                                  (!m.IsPrivate || (m.IsPrivate && (conv.BuyerUserId == currentUserId || conv.SellerUserId == currentUserId || conv.AdminUserId == currentUserId)))
                              );

            return new ConversationDto
            {
                Id = conv.Id,
                BuyerUserId = conv.BuyerUserId,
                BuyerUsername = buyerUsername,
                SellerUserId = conv.SellerUserId,
                SellerUsername = sellerUsername,
                AdminUserId = conv.AdminUserId,
                AdminUsername = adminUsername,
                StoreId = conv.StoreId ?? 0,
                StoreName = storeName,
                OrderId = conv.OrderId,
                ProductId = conv.ProductId,
                ProductName = productName,
                TicketId = conv.TicketId,
                CreatedAt = conv.CreatedAt,
                LastMessage = lastMessageDto,
                UnreadMessagesCount = unreadCount
            };
        }

        public async Task<IEnumerable<MessageDto>> GetAllMessagesForConversationAsync(int conversationId, string requestingUserId, bool isAdminFromController, int page = 1, int pageSize = 30)
        {
            _logger.LogInformation("[GetAllMessages] User {UID} (IsAdminCtrl: {IsAdmin}) fetching ALL messages for Conversation {CID}, Page {P}, Size {PS}",
                requestingUserId, isAdminFromController, conversationId, page, pageSize);

            var conversation = await _context.Conversations
                                      .AsNoTracking()
                                      .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
            {
                _logger.LogWarning("[GetAllMessages] Conversation {ConversationId} not found.", conversationId);
                return Enumerable.Empty<MessageDto>();
            }

            bool isDirectParticipant = conversation.BuyerUserId == requestingUserId ||
                                      conversation.SellerUserId == requestingUserId ||
                                      conversation.AdminUserId == requestingUserId;

            if (!isAdminFromController && !isDirectParticipant)
            {
                _logger.LogWarning("[GetAllMessages] User {RequestingUserId} unauthorized access to Conversation {ConversationId}", requestingUserId, conversationId);
                throw new UnauthorizedAccessException("User does not have access to this conversation.");
            }

            var query = _context.Messages
                .Where(m => m.ConversationId == conversationId);


            if (isAdminFromController && !isDirectParticipant)
            {
                query = query.Where(m => !m.IsPrivate);
                _logger.LogDebug("[GetAllMessages] Admin {AdminUserId} (not participant) viewing non-private messages for Conversation {ConversationId}", requestingUserId, conversationId);
            }

            query = query.OrderByDescending(m => m.SentAt)
                         .Skip((page - 1) * pageSize)
                         .Take(pageSize)
                         .AsNoTracking();

            var messages = await query.ToListAsync();
            _logger.LogInformation("[GetAllMessages] Retrieved {MessageCount} messages for Conversation {ConversationId}", messages.Count, conversationId);

            var senderIds = messages.Select(m => m.SenderUserId).Distinct().ToList();
            var senders = new Dictionary<string, string?>();
            if (senderIds.Any())
            {
                senders = await _userManager.Users
                                    .Where(u => senderIds.Contains(u.Id))
                                    .ToDictionaryAsync(u => u.Id, u => u.UserName);
            }

            return messages.Select(m => new MessageDto
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                SenderUserId = m.SenderUserId,
                SenderUsername = senders.TryGetValue(m.SenderUserId, out var username) ? username : "Nepoznat",
                Content = m.Content,
                SentAt = m.SentAt,
                ReadAt = m.ReadAt,
                IsPrivate = m.IsPrivate
            }).Reverse();
        }
    }
}