using System.ComponentModel.DataAnnotations;

namespace MarketingAnalytics.Models
{
    public class AdStatsDto
    {
        [Required(ErrorMessage = "UserId je obavezan.")]
        public string UserId { get; set; } = string.Empty;

        [Required(ErrorMessage = "AdvertisementId je obavezan.")]
        [Range(1, int.MaxValue, ErrorMessage = "AdvertisementId mora biti pozitivan broj.")]
        public int AdvertisementId { get; set; }
    }
}