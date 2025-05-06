using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Review.Models
{
    public class ReviewModel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public required string BuyerId { get; set; }

        [Required]
        public required int StoreId { get; set; }

        [Required]
        public required int OrderId { get; set; }

        public int Rating { get; set; }

        public string Comment { get; set; } = string.Empty;

        public DateTime DateTime { get; set; }

        public bool IsApproved { get; set; } = true;

        public ReviewResponse? Response { get; set; }
    }
}