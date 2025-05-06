using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Review.Models
{
    public class ReviewResponse
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int ReviewId { get; set; } // Foreign key to Review

        public ReviewModel Review { get; set; } = null!;

        public required string Response { get; set; }

        public DateTime DateTime { get; set; }
    }
}