using Store.Models;

namespace Store.Interface
{
    public interface IGeographyService
    {
        Task<IEnumerable<Region>> GetAllRegionsAsync();
        Task<Region?> GetRegionByIdAsync(int id);
        Task<Region> CreateRegionAsync(Region region);
        Task<bool> UpdateRegionAsync(Region region);
        Task<bool> DeleteRegionAsync(int id);
        Task<IEnumerable<Place>> GetAllPlacesAsync();
        Task<Place?> GetPlaceByIdAsync(int id);
        Task<Place> CreatePlaceAsync(Place place);
        Task<bool> UpdatePlaceAsync(Place place);
        Task<bool> DeletePlaceAsync(int id);
        Task<IEnumerable<Place>> GetAllPlacesInRegionAsync(int regionId);

    }
}