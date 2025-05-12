using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Chat.Dtos
{
    public class MessageDto
    {
        public int Id { get; set; }
        public int ConversationId { get; set; }
        public required string SenderUserId { get; set; }
        public string? SenderUsername { get; set; }
        public required string Content { get; set; }
        public DateTime SentAt { get; set; }
        public DateTime? ReadAt { get; set; }
        public bool IsPrivate { get; set; }
        // PreviousMessageId ne treba u DTO ako ga ne koristimo
    }

    public class CreateMessageDto
    {
        [Required]
        public int ConversationId { get; set; }

        [Required]
        [MaxLength(4000)]
        public required string Content { get; set; }

        public bool IsPrivate { get; set; } = false;
    }

    public class ConversationDto
    {
        public int Id { get; set; }
        public required string BuyerUserId { get; set; }
        public string? BuyerUsername { get; set; }
        public required string SellerUserId { get; set; }
        public string? SellerUsername { get; set; }
        public int StoreId { get; set; }
        public string? StoreName { get; set; }
        public int? OrderId { get; set; }
        public int? ProductId { get; set; }
        public string? ProductName { get; set; }
        public DateTime CreatedAt { get; set; }
        public MessageDto? LastMessage { get; set; }
        public int UnreadMessagesCount { get; set; } = 0;
    }

    public class FindOrCreateConversationDto
    {

        public string targetUserId { get; set; } = "";
        [Required]
        public int StoreId { get; set; }
        public int? OrderId { get; set; } = null;
        public int? ProductId { get; set; } = null;
    }
}