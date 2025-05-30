using System.Runtime.CompilerServices;
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
        Task<UserActivity> CreateUserActivityAsync(UserActivity userActivity);
        Task<ICollection<UserActivity>> GetUserActivitiesByUserId(string id);
        Task<Clicks?> RecordClickAsync(AdStatsDto clickDto);
        Task<Views?> RecordViewAsync(AdStatsDto viewDto);
        Task<Conversions?> RecordConversionAsync(AdStatsDto conversionDto);
        Task<ICollection<DateTime>> GetClicksTimestampsAsync(int advertismentId, DateTime? from = null, DateTime? to = null);
        Task<ICollection<DateTime>> GetViewsTimestampsAsync(int advertismentId, DateTime? from = null, DateTime? to = null);
        Task<ICollection<DateTime>> GetConversionsTimestampsAsync(int advertismentId, DateTime? from = null, DateTime? to = null);
        List<string> AdTriggerToString(int triggers);
        int AdTriggerFromStrings(List<string> interactions);
    }
}