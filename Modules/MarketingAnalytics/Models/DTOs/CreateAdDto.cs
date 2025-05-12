using System.ComponentModel.DataAnnotations;
using MarketingAnalytics.Models;
using MarketingAnalytics.Services.DTOs;

namespace MarketingAnalytics.DTOs
{


    public class CreateAdDto
    {
        [Required]
        public string SellerId { get; set; } = string.Empty; // Make non-nullable in DTO if required

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        public string AdType { get; set; }
        public List<string> Triggers { get; set; }
        public decimal ClickPrice { get; set; }
        public decimal ViewPrice { get; set; }
        public decimal ConversionPrice { get; set; }

        // Optional: Add other Advertisment properties if they are set during creation
        // public bool IsActive { get; set; } // Often determined by Start/End time

        [Required]
        public List<AdDataInputDto> AdDataItems { get; set; } = new List<AdDataInputDto>();
    }
}