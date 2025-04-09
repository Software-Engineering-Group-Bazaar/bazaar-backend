using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Store.Models;

namespace Catalog.Models
{
    public class Product
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public required string Name { get; set; } = string.Empty;

        // Foreign Key to ProductCategory
        [Required]
        public required ProductCategory ProductCategory { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public required decimal RetailPrice { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public required decimal WholesalePrice { get; set; }

        [Column(TypeName = "decimal(10, 3)")]
        public decimal? Weight { get; set; }

        [StringLength(20)]
        public string? WeightUnit { get; set; }

        [Column(TypeName = "decimal(10, 3)")]
        public decimal? Volume { get; set; }

        [StringLength(20)]
        public string? VolumeUnit { get; set; }

        [Required]
        public required int StoreId { get; set; }
    }
}