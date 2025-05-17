using System.Threading.Tasks;
using Delivery.Interfaces;
using Delivery.Models;
using Microsoft.EntityFrameworkCore;

namespace Delivery.Services
{
    class RoutesService : IRoutesService
    {
        public DeliveryDbContext _context;
        public ILogger<RoutesService> _logger;

        public RoutesService(DeliveryDbContext context, ILogger<RoutesService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<DeliveryRoute>> GetAllDeliveryRoutes()
        {
            return await _context.Routes.ToListAsync();
        }

        public async Task<DeliveryRoute?> GetRouteByIdAsync(int id)
        {
            var route = await _context.Routes.FirstOrDefaultAsync(r => r.Id == id);
            return route;
        }

        public async Task<List<DeliveryRoute>> GetRoutesFromOrders(List<int> orders)
        {
            var routes = await _context.Routes.Where(
                r => orders.OrderBy(x => x).SequenceEqual(r.OrderIds.OrderBy(x => x))
            ).ToListAsync();
            return routes;
        }

        public async Task DeleteRouteAsync(int id)
        {
            var route = await _context.Routes.FirstOrDefaultAsync(r => r.Id == id);
            if (route is null)
                throw new InvalidDataException($"route {id} does not exist");
            _context.Routes.Remove(route);
            await _context.SaveChangesAsync();
        }

        public async Task<DeliveryRoute> CreateRoute(string routeCreatorId, List<int> orderIds, DeliveryRouteData routeData)
        {
            var route = new DeliveryRoute
            {
                OwnerId = routeCreatorId,
                OrderIds = orderIds,
                RouteData = routeData
            };
            _context.Add(route);
            await _context.SaveChangesAsync();
            return route;
        }

        public async Task<DeliveryRoute> UpdateRouteDataAsync(int id, DeliveryRouteData routeData)
        {
            var route = await _context.Routes.FirstOrDefaultAsync(r => r.Id == id);
            if (route is null)
                throw new InvalidDataException($"route {id} does not exist");
            if (route.RouteData.Hash != routeData.Hash)
            {
                route.RouteData = routeData;
                await _context.SaveChangesAsync();
            }
            return route;

        }

        public async Task<DeliveryRoute> UpdateRouteOrdersAsync(int id, List<int> orderIds)
        {
            var route = await _context.Routes.FirstOrDefaultAsync(r => r.Id == id);
            if (route is null)
                throw new InvalidDataException($"route {id} does not exist");
            route.OrderIds = orderIds;
            await _context.SaveChangesAsync();
            return route;
        }
    }
}