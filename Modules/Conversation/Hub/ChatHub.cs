using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Chat.Dtos; // Za CreateMessageDto
using Chat.Interfaces;
using Conversation.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Chat.Hubs // Prilagodi namespace
{
    [Authorize] // Zahtijeva autentifikaciju za konekciju na Hub
    public class ChatHub : Hub
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatHub> _logger;

        private readonly ConversationDbContext _context;

        // Opciono: Za mapiranje ConnectionId na UserId ako trebaš naprednije praćenje
        // private static readonly ConnectionMapping<string> _connections = new ConnectionMapping<string>();

        public ChatHub(IChatService chatService, ILogger<ChatHub> logger, ConversationDbContext context)
        {
            _chatService = chatService;
            _logger = logger;
            _context = context;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier; // Dohvati UserId iz tokena
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User connected to ChatHub without identifier. Aborting connection.");
                Context.Abort(); // Prekini konekciju ako nema User ID-a
                return;
            }

            _logger.LogInformation("User {UserId} connected to ChatHub with ConnectionId {ConnectionId}", userId, Context.ConnectionId);
            // _connections.Add(userId, Context.ConnectionId);

            // Opciono: Automatski dodaj korisnika u grupe za sve njegove konverzacije
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

            // Opciono: Ukloni iz svih grupa (SignalR bi ovo trebao raditi automatski za konekciju,
            // ali ako pratiš grupe eksplicitno, ovdje bi bila logika za to)

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
                // Možeš poslati poruku klijentu da je uspješno ušao u sobu
                // await Clients.Caller.SendAsync("JoinedConversationSuccess", conversationId);
            }
            else
            {
                _logger.LogWarning("User {UserId} failed to join group {GroupName} - unauthorized.", userId, GetConversationGroupName(conversationId));
                // Možeš poslati poruku klijentu o neuspjehu
                // await Clients.Caller.SendAsync("JoinedConversationFailed", conversationId, "Unauthorized");
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
        public async Task SendMessage(CreateMessageDto messageDto) // Koristi DTO za prijem
        {
            var senderUserId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(senderUserId) || messageDto == null)
            {
                _logger.LogWarning("SendMessage failed: SenderUserId is null or messageDto is null.");
                // Možeš poslati grešku nazad klijentu ako želiš
                // await Clients.Caller.SendAsync("SendMessageFailed", "Invalid request.");
                return;
            }

            _logger.LogInformation("Hub: User {SenderUserId} attempting to send message to Conversation {ConversationId}: '{Content}' (IsPrivate: {IsPrivate})",
                senderUserId, messageDto.ConversationId, messageDto.Content.Substring(0, Math.Min(30, messageDto.Content.Length)), messageDto.IsPrivate);

            try
            {
                // 1. Sačuvaj poruku u bazi koristeći servis
                // Servis će uraditi autorizaciju da li sender pripada konverzaciji
                MessageDto? savedMessageDto = await _chatService.SaveMessageAsync(
                    senderUserId,
                    messageDto.ConversationId,
                    messageDto.Content,
                    messageDto.IsPrivate
                );

                if (savedMessageDto == null)
                {
                    _logger.LogError("Hub: Failed to save message from {SenderUserId} to Conversation {ConversationId}. ChatService returned null.", senderUserId, messageDto.ConversationId);
                    // await Clients.Caller.SendAsync("SendMessageFailed", "Could not save message.");
                    return;
                }

                // 2. Dohvati primaoce iz konverzacije
                var conversation = await _context.Conversations // Pretpostavka da ChatHub ima ChatDbContext injektiran, ili dohvati preko servisa
                                        .AsNoTracking()
                                        .FirstOrDefaultAsync(c => c.Id == messageDto.ConversationId);

                if (conversation == null)
                {
                    _logger.LogError("Hub: Conversation {ConversationId} not found after saving message.", messageDto.ConversationId);
                    return;
                }

                // Odredi ko je primalac
                string recipientUserId = conversation.BuyerUserId == senderUserId ? conversation.SellerUserId : conversation.BuyerUserId;

                // 3. Pošalji poruku primaocu (ako je online i konektovan na ovaj Hub)
                // Koristi grupu za slanje svima u konverzaciji
                _logger.LogInformation("Hub: Sending 'ReceiveMessage' to group {GroupName} for message ID {MessageId}", GetConversationGroupName(messageDto.ConversationId), savedMessageDto.Id);
                await Clients.Group(GetConversationGroupName(messageDto.ConversationId))
                             .SendAsync("ReceiveMessage", savedMessageDto); // Pošalji cijeli MessageDto

                // 4. Opciono: Pošalji potvrdu pošiljaocu
                // await Clients.Caller.SendAsync("MessageSentConfirmation", clientTempId, savedMessageDto);
                _logger.LogInformation("Hub: Message ID {MessageId} sent successfully to group {GroupName}.", savedMessageDto.Id, GetConversationGroupName(messageDto.ConversationId));
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Hub: Unauthorized attempt to send message by User {SenderUserId} to Conversation {ConversationId}.", senderUserId, messageDto.ConversationId);
                // await Clients.Caller.SendAsync("SendMessageFailed", "Unauthorized to send message to this conversation.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hub: Error sending message from {SenderUserId} to Conversation {ConversationId}.", senderUserId, messageDto.ConversationId);
                // await Clients.Caller.SendAsync("SendMessageFailed", "An error occurred.");
            }
        }

        // Pomoćna metoda za generisanje imena grupe
        private string GetConversationGroupName(int conversationId)
        {
            return $"conversation_{conversationId}";
        }
    }

    // Opciono: Klasa za mapiranje ConnectionId na UserID ako treba naprednije praćenje
    // public class ConnectionMapping<T> { ... }
}