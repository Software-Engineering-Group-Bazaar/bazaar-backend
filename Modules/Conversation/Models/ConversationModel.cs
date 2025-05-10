using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Order.Models;
using Store.Models;
using Users.Models;

namespace Conversation.Models
{

    public class Conversation
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(450)]
        public required string BuyerUserId { get; set; }

        [Required]
        [MaxLength(450)]
        public required string SellerUserId { get; set; }

        [Required]
        public int StoreId { get; set; }

        public int? ProductId { get; set; }

        public int? OrderId { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? LastMessageId { get; set; }
    }
}
