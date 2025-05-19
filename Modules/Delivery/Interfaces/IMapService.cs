using Delivery.Navigation.Models;

namespace Delivery.Navigation.Interfaces
{
    public interface IMapService
    {
        Task<DirectionsResponse?> GetDirectionsAsync(
            string origin,
            string destination,
            IEnumerable<string>? waypoints = null,
            string travelMode = "driving",
            bool alternatives = false,
            bool optimizeWaypoints = false);
        Task<GeoData> GetGeoDataAsync(string address)
    }
}