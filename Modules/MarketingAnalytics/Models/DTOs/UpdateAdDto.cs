using System.ComponentModel.DataAnnotations;
using MarketingAnalytics.Models;
using MarketingAnalytics.Services.DTOs;

namespace MarketingAnalytics.DTOs
{


    public class UpdateAdDto
    {
        // Only include fields that are allowed to be updated
        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }
        public decimal ClickPrice { get; set; }
        public decimal ViewPrice { get; set; }
        public decimal ConversionPrice { get; set; }
        public List<string> Triggers { get; set; }
        public string AdType { get; set; }

        public bool? IsActive { get; set; } // Optional: Allow explicit setting

        // Allows adding NEW AdData items during an Advertisment update
        public List<AdDataInputDto>? NewAdDataItems { get; set; } = null;
    }
}