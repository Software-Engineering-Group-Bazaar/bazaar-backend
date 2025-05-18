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

        public async Task<DeliveryRoute> GetRoutesFromOrders(List<int> orders)
        {
            if (orders == null || !orders.Any())
            {
                return null;
            }

            int orderCount = orders.Count;
            var query = _context.Routes.AsQueryable();

            // Filtriraj rute koje imaju tačno isti broj OrderId-eva
            // Za ovo, Npgsql provider mora da podržava .Count na nizu (što obično radi za integer[])
            // Ako ne radi, ovaj deo će morati da se prebaci na klijentsko filtriranje posle dohvatanja
            query = query.Where(r => r.OrderIds.Count >= orderCount);

            // Filtriraj rute koje sadrže SVE ID-eve iz ulazne liste
            // Ovo se prevodi u SQL `WHERE OrderIds @> ARRAY[id1, id2, ...]` (contains operator)
            foreach (var orderId in orders)
            {
                query = query.Where(r => r.OrderIds.Contains(orderId));
            }

            // Izvrši upit
            var potentialRoutes = await query.ToListAsync();

            // Pošto Contains operator (@>) ne garantuje da NEMA drugih ID-eva,
            // moramo još jednom proveriti na klijentu da su liste IDENTIČNE.
            // Ovo je neophodno jer SQL nema direktan ekvivalent za SequenceEqual na nizovima
            // na način koji bi EF Core mogao da prevede u svim slučajevima.
            //var sortedOrders = orders.OrderBy(x => x).ToList();
            //var matchingRoutes = potentialRoutes
            //    .Where(r => r.OrderIds != null && sortedOrders.SequenceEqual(r.OrderIds.OrderBy(x => x)))
            //   .ToList();

            return potentialRoutes.FirstOrDefault();
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