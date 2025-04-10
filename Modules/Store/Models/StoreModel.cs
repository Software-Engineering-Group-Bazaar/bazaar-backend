using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Store.Models
{
    public class StoreModel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string SellerUserId { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public StoreCategory Category { get; set; } = null!;

        public bool IsActive { get; set; } = true;

        public string? Description { get; set; }

        [Required]
        public string Address { get; set; } = string.Empty;
    }
}