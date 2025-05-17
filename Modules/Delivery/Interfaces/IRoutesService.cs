using Delivery.Models;

namespace Delivery.Interfaces
{
    public interface IRoutesService
    {
        Task<List<DeliveryRoute>> GetAllDeliveryRoutes();
        Task<DeliveryRoute?> GetRouteByIdAsync(int id);
        Task<DeliveryRoute> GetRoutesFromOrders(List<int> orders);
        Task DeleteRouteAsync(int id);
        Task<DeliveryRoute> CreateRoute(string routeCreatorId, List<int> orderIds, DeliveryRouteData routeData);
        Task<DeliveryRoute> UpdateRouteDataAsync(int id, DeliveryRouteData routeData);
        Task<DeliveryRoute> UpdateRouteOrdersAsync(int id, List<int> orderIds);


    }
}