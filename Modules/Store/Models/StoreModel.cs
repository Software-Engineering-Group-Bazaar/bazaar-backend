using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Store.Models
{
    public class StoreModel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id { get; set; }

        [Required]
        [MaxLength(255)]
        public required string name { get; set; }
        public required StoreCategory category { get; set; }
        public required bool isActive { get; set; } = true;
        public required string address { get; set; } //odsad se adresa tumaci kao ulica; Broj
        public string? description { get; set; }
        public Place? place { get; set; } = null!; // tumaci kao grad
        public int placeId { get; set; }

    }
}