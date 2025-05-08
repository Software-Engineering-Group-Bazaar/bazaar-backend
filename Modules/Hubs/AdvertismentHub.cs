// MarketingAnalytics/Hubs/AdvertisementHub.cs
using System.Threading.Tasks;
using MarketingAnalytics.Dtos; // Your DTO namespace
using MarketingAnalytics.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace MarketingAnalytics.Hubs
{
    // Hub for broadcasting advertisement changes
    public class AdvertisementHub : Hub
    {
        // Methods below are the "contracts" that the server will invoke on clients.
        // Clients need to implement listeners for these method names.
        // Typically, when broadcasting FROM the server via IHubContext, these hub methods themselves don't need bodies.

        public async Task SendAdvertisementCreated(AdvertismentDto newAdvertisement)
        {
            // This is invoked by the server using _hubContext.Clients.All.SendAsync("AdvertisementCreated", ...)
            // No server-side processing needed here when broadcasting.
            await Task.CompletedTask;
        }

        public async Task SendAdvertisementUpdated(AdvertismentDto updatedAdvertisement)
        {
            await Task.CompletedTask;
        }

        public async Task SendAdvertisementDeleted(int advertisementId)
        {
            await Task.CompletedTask;
        }

        // Optional: For logging connections
        public override Task OnConnectedAsync()
        {
            _logger.LogInformation("Client connected to AdvertisementHub: {ConnectionId}", Context.ConnectionId); // Assuming _logger injected if needed
            return base.OnConnectedAsync();
        }

        // Optional: For logging disconnections
        public override Task OnDisconnectedAsync(System.Exception? exception)
        {
            _logger.LogInformation("Client disconnected from AdvertisementHub: {ConnectionId}. Error: {Error}", Context.ConnectionId, exception?.Message); // Assuming _logger injected if needed
            return base.OnDisconnectedAsync(exception);
        }

        // Example Logger Injection (if needed in Hub directly)
        private readonly ILogger<AdvertisementHub> _logger;
        public AdvertisementHub(ILogger<AdvertisementHub> logger)
        {
            _logger = logger;
        }
    }
}