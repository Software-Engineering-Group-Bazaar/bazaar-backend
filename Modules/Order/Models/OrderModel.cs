using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Order.Models
{
    public class OrderModel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public ICollection<OrderItem> OrderItems { get; } = new List<OrderItem>();
        public string BuyerId { get; set; }
        public int StoreId { get; set; }
        public OrderStatus Status { get; set; } = OrderStatus.Requested;
        public DateTime Time { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? Total { get; set; }
        public int AddressId { get; set; }
        public bool AdminDelivery { get; set; } = false;
        public DateTime? ExpectedReadyAt { get; set; } = null;
    }
}