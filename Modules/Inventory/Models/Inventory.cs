using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Catalog.Models;
using Microsoft.EntityFrameworkCore;
using Store.Models;

namespace Inventory.Models
{
    [Index(nameof(ProductId), nameof(StoreId), IsUnique = true)]
    public class Inventory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        public int StoreId { get; set; }

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "Quantity cannot be negative.")]
        public int Quantity { get; set; } = 0;

        [Required]
        public bool OutOfStock { get; set; } = true;

        [Required]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}