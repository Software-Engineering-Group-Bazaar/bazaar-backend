using MarketingAnalytics.Models;
using MarketingAnalytics.Services.DTOs;

namespace MarketingAnalytics.Interfaces
{
    public interface IAdService
    {
        Task<IEnumerable<Advertisment>> GetAllAdvertisementsAsync();
        Task<Advertisment?> GetAdvertisementByIdAsync(int id);
        Task<Advertisment> CreateAdvertismentAsync(CreateAdvertismentRequestDto request);
        Task<bool> DeleteAdvertismentAsync(int advertismentId);
        Task<Advertisment?> UpdateAdvertismentAsync(int advertismentId, UpdateAdvertismentRequestDto request);
        Task<AdData?> UpdateAdDataAsync(int adDataId, UpdateAdDataRequestDto request);
        Task<bool> DeleteAdDataAsync(int adDataId);
    }
}