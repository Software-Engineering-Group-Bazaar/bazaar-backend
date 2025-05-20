using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Chat.Dtos;
using Chat.Interfaces;
using Conversation.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Chat.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatHub> _logger;

        private readonly ConversationDbContext _context;

        public ChatHub(IChatService chatService, ILogger<ChatHub> logger, ConversationDbContext context)
        {
            _chatService = chatService;
            _logger = logger;
            _context = context;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User connected to ChatHub without identifier. Aborting connection.");
                Context.Abort();
                return;
            }

            _logger.LogInformation("User {UserId} connected to ChatHub with ConnectionId {ConnectionId}", userId, Context.ConnectionId);
            // _connections.Add(userId, Context.ConnectionId);

            var conversations = await _chatService.GetConversationsForUserAsync(userId);
            foreach (var conv in conversations)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GetConversationGroupName(conv.Id));
                _logger.LogInformation("User {UserId} ConnectionId {ConnectionId} added to group {GroupName}", userId, Context.ConnectionId, GetConversationGroupName(conv.Id));
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;
            _logger.LogInformation("User {UserId} disconnected from ChatHub with ConnectionId {ConnectionId}. Exception: {Exception}",
                                   userId ?? "N/A", Context.ConnectionId, exception?.Message ?? "N/A");
            // _connections.Remove(userId, Context.ConnectionId);

            await base.OnDisconnectedAsync(exception);
        }

        // Klijent poziva ovu metodu da se pridruži specifičnoj sobi/konverzaciji
        public async Task JoinConversation(int conversationId)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId)) return;

            if (await _chatService.CanUserAccessConversationAsync(userId, conversationId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GetConversationGroupName(conversationId));
                _logger.LogInformation("User {UserId} ConnectionId {ConnectionId} joined group {GroupName}", userId, Context.ConnectionId, GetConversationGroupName(conversationId));
                await Clients.Caller.SendAsync("JoinedConversationSuccess", conversationId);
            }
            else
            {
                _logger.LogWarning("User {UserId} failed to join group {GroupName} - unauthorized.", userId, GetConversationGroupName(conversationId));
                await Clients.Caller.SendAsync("JoinedConversationFailed", conversationId, "Unauthorized");
            }
        }

        public async Task LeaveConversation(int conversationId)
        {
            var userId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(userId)) return;

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetConversationGroupName(conversationId));
            _logger.LogInformation("User {UserId} ConnectionId {ConnectionId} left group {GroupName}", userId, Context.ConnectionId, GetConversationGroupName(conversationId));
        }

        // Klijent poziva ovu metodu da pošalje poruku
        public async Task SendMessage(CreateMessageDto messageDto)
        {
            var senderUserId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(senderUserId) || messageDto == null || string.IsNullOrWhiteSpace(messageDto.Content))
            {
                _logger.LogWarning("SendMessage failed: SenderUserId is null or messageDto is invalid. Sender: {Sender}, DTO: {@DTO}", senderUserId, messageDto);
                await Clients.Caller.SendAsync("SendMessageFailed", messageDto?.ConversationId ?? 0, "Invalid request: Message content or conversation ID might be missing.");
                return;
            }

            _logger.LogInformation("Hub: User {SenderUserId} attempting to send message to Conversation {ConversationId}. Content snippet: '{ContentSnippet}' (IsPrivate: {IsPrivate})",
                senderUserId, messageDto.ConversationId, messageDto.Content.Substring(0, Math.Min(30, messageDto.Content.Length)), messageDto.IsPrivate);

            try
            {
                MessageDto? savedMessageDto = await _chatService.SaveMessageAsync(
                    senderUserId,
                    messageDto.ConversationId,
                    messageDto.Content,
                    messageDto.IsPrivate
                );

                if (savedMessageDto == null)
                {
                    _logger.LogError("Hub: Failed to save message from {SenderUserId} to Conversation {ConversationId}. ChatService returned null, which is unexpected.", senderUserId, messageDto.ConversationId);
                    await Clients.Caller.SendAsync("SendMessageFailed", messageDto.ConversationId, "Failed to process message on the server.");
                    return;
                }

                string groupName = GetConversationGroupName(messageDto.ConversationId);
                _logger.LogInformation("Hub: Broadcasting 'ReceiveMessage' to group {GroupName} with MessageDto: {@MessageDto}", groupName, savedMessageDto);

                await Clients.Group(groupName).SendAsync("ReceiveMessage", savedMessageDto);

                _logger.LogInformation("Hub: Message ID {MessageId} (ConvID: {ConvID}) successfully broadcast to group {GroupName}.", savedMessageDto.Id, savedMessageDto.ConversationId, groupName);

                await Clients.Caller.SendAsync("MessageSentSuccessfully", savedMessageDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Hub: Unauthorized message send attempt by User {SenderUserId} to Conversation {ConversationId}.", senderUserId, messageDto.ConversationId);
                await Clients.Caller.SendAsync("SendMessageFailed", messageDto.ConversationId, "Unauthorized to send message to this conversation.");
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Hub: Conversation {ConversationId} not found during SendMessage by User {SenderUserId}.", messageDto.ConversationId, senderUserId);
                await Clients.Caller.SendAsync("SendMessageFailed", messageDto.ConversationId, "Conversation not found.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hub: Error processing SendMessage from User {SenderUserId} to Conversation {ConversationId}.", senderUserId, messageDto.ConversationId);
                await Clients.Caller.SendAsync("SendMessageFailed", messageDto.ConversationId, "An internal error occurred while sending the message.");
            }
        }

        private string GetConversationGroupName(int conversationId)
        {
            return $"conversation_{conversationId}";
        }
    }

    // Opciono: Klasa za mapiranje ConnectionId na UserID ako treba naprednije praćenje
    // public class ConnectionMapping<T> { ... }
}