using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Order.Models
{
    public class OrderModel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int StoreId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Status { get; set; } = OrderStatus.Pending.ToString();

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}