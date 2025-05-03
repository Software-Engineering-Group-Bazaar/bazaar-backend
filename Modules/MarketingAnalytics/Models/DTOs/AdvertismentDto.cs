using System;
using System.Collections.Generic;
using MarketingAnalytics.Dtos;

// Namespace for Data Transfer Objects
namespace MarketingAnalytics.DTOs
{
    /// <summary>
    /// Data Transfer Object for Advertisment information.
    /// Used for transferring advertisement data, often across API boundaries.
    /// </summary>
    public class AdvertismentDto
    {
        public int Id { get; set; }
        public string SellerId { get; set; } = string.Empty; // Initialize non-nullable string
        public int Views { get; set; }
        public int Clicks { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsActive { get; set; }

        // Include related AdData using its own DTO
        // This assumes AdDataDto is defined in the same namespace or appropriate using directives are added.
        public List<AdDataDto> AdData { get; set; } = new List<AdDataDto>();
    }
}