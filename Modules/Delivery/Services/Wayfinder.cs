using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Delivery.Interfaces;
using Delivery.Models;
using Delivery.Navigation.Interfaces;
using Delivery.Navigation.Models;
using Order.Interface;
using Order.Models;
using Store.Interface;
using Store.Models;
using Users.Interface;
using Users.Interfaces;
using Users.Models;
using Route = Delivery.Navigation.Models.Route;

namespace Delivery.Navigation.Services
{
    /// <summary>
    /// Wayfinder — Knows where everything is. Knows how to get anywhere.
    /// </summary>
    class Wayfinder
    {
        private readonly ILogger<Wayfinder> _logger;
        private readonly IAddressService _addressService;
        private readonly IStoreService _storeService;
        private readonly IUserService _userService;
        private readonly IOrderService _orderService;
        private readonly IRoutesService _routeService;
        private readonly IMapService _mapService;

        public Wayfinder(ILogger<Wayfinder> logger, IAddressService addressService, IStoreService storeService, IUserService userService, IOrderService orderService, IRoutesService routesService, IMapService mapService)
        {
            _logger = logger;
            _addressService = addressService;
            _storeService = storeService;
            _userService = userService;
            _orderService = orderService;
            _routeService = routesService;
            _mapService = mapService;
        }

        public async Task<List<Post>> OrdersConversion(List<int> orderIds)
        {
            var res = new List<Post>();
            foreach (var id in orderIds)
            {
                var order = await _orderService.GetOrderByIdAsync(id);
                var r = await OrderConverter(order);
                res.Add(r);
            }
            return res;
        }

        public async Task<Post> OrderConverter(OrderModel order)
        {
            var addr = await _addressService.GetAddressByIdAsync(order.AddressId);
            var s = _storeService.GetStoreById(order.StoreId);
            var f = await StoreConverterAsync(s);
            return new Post
            {
                To = AddressConverter(addr),
                From = f
            };
        }

        public async Task<GeoData> StoreConverterAsync(StoreModel store)
        {
            var address = store.address;
            var place = store.place.Name;

            _logger.LogDebug($"StoreConverterAsync: {store.id} | {address} | {store.placeId} | {place}");
            return await _mapService.GetGeoDataAsync($"{address}, ${place}");
        }

        public async Task<DeliveryRoute> OrderPathFind(string userId, List<int> orderIds)
        {
            var route = await _routeService.GetRoutesFromOrders(orderIds);
            if (route != null)
            {
                return route;
            }
            var letters = await OrdersConversion(orderIds);
            var locations = letters.Select(p => p.From).ToList();
            locations.AddRange(letters.Select(p => p.To).ToList());
            IEnumerable<string>? waypoints;
            try
            {
                waypoints = [.. locations[1..^1].Select(l => l.StreetAddress)];
            }
            catch (ArgumentOutOfRangeException)
            {
                waypoints = null;
            }
            var googledata = await _mapService.GetDirectionsAsync(locations[0].StreetAddress, locations.Last().StreetAddress);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true, // For pretty-printed JSON
                // PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // If your C# props are PascalCase and you want camelCase JSON
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull // To omit null properties from JSON
            };
            var firstRoute = googledata.Routes[0];
            var routeJson = JsonSerializer.Serialize(firstRoute, options);

            route = await _routeService.CreateRoute(userId, orderIds, new DeliveryRouteData
            {
                Data = routeJson,
                Hash = "aa"
            });

            return route;
        }
        private GeoData AddressConverter(Address address)
        {
            return new GeoData
            {
                Latitude = address.Latitude,
                Longitude = address.Latitude,
                StreetAddress = address.StreetAddress
            };
        }
    }
}