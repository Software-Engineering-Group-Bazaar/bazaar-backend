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
        public string Name { get; set; } = string.Empty;

        public int ProductCategoryId { get; set; }

        // Foreign Key to ProductCategory
        [Required]
        public ProductCategory ProductCategory { get; set; } = null!;

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal RetailPrice { get; set; }

        public int? WholesaleThreshold { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? WholesalePrice { get; set; }

        [Column(TypeName = "decimal(10, 3)")]
        public decimal? Weight { get; set; }

        [StringLength(20)]
        public string? WeightUnit { get; set; }

        [Column(TypeName = "decimal(10, 3)")]
        public decimal? Volume { get; set; }

        [StringLength(20)]
        public string? VolumeUnit { get; set; }

        [Required]
        public int StoreId { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public virtual ICollection<ProductPicture> Pictures { get; set; } =
            new List<ProductPicture>();
        public double PointRate { get; set; } = 1;
    }
}
