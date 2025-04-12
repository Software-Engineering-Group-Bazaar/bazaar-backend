using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Models
{
    public class ProductPicture
    {
        // [Key]
        // [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        // public int Id { get; set; }

        [Key]
        [StringLength(255)]
        public string Url { get; set; } = string.Empty;
        public int ProductId { get; set; }

        public Product Product { get; set; } = null!;

    }
}