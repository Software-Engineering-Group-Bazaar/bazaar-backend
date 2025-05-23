// MarketingAnalytics/Hubs/AdvertisementHub.cs
using System.Threading.Tasks;
using MarketingAnalytics.Dtos; // Your DTO namespace
using MarketingAnalytics.DTOs;
using MarketingAnalytics.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MarketingAnalytics.Hubs
{
    // Hub for broadcasting advertisement changes
    [Authorize(Roles = "Admin")]
    public class AdvertisementHub : Hub
    {
        public const string AdminGroup = "Admins";

        // Called when a new connection is established with the hub.
        public override async Task OnConnectedAsync()
        {
            // Check if the connected user has the "Admin" role
            // Ensure your authentication is set up correctly for HttpContext.User to be populated.
            if (Context.User?.IsInRole("Admin") == true)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);
                // Optional: Log or send a welcome message to the admin
                // await Clients.Caller.SendAsync("AdminWelcomeMessage", "Welcome Admin! You will receive ad updates.");
            }
            await base.OnConnectedAsync();
        }

        // Called when a connection with the hub is terminated.
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // If they were in the admin group, SignalR automatically removes them on disconnect,
            // but explicit removal can be good practice if you have complex group logic.
            if (Context.User?.IsInRole("Admin") == true)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, AdminGroup);
            }
            await base.OnDisconnectedAsync(exception);
        }

        // This is a server-side method that your application logic will call
        // It's not meant to be called directly by clients.
        public async Task SendAdUpdateToAdmins(Advertisment updatedAd)
        {
            // Send the update to all clients in the "Admins" group
            var dto = new AdvertismentDto
            {
                Id = updatedAd.Id,
                SellerId = updatedAd.SellerId,
                StartTime = updatedAd.StartTime,
                EndTime = updatedAd.EndTime,
                IsActive = updatedAd.IsActive,
                Views = updatedAd.Views,
                ViewPrice = updatedAd.ViewPrice,
                Clicks = updatedAd.Clicks,
                ClickPrice = updatedAd.ClickPrice,
                Conversions = updatedAd.Conversions,
                ConversionPrice = updatedAd.ConversionPrice,
                AdType = updatedAd.AdType.ToString(),
                Triggers = AdTriggerToString(updatedAd.Triggers),
                AdData = updatedAd.AdData.Select(ad => new AdDataDto
                {
                    Id = ad.Id,
                    StoreId = ad.StoreId,
                    ImageUrl = ad.ImageUrl,
                    Description = ad.Description,
                    ProductId = ad.ProductId
                }).ToList()
            };
            await Clients.Group(AdminGroup).SendAsync("ReceiveAdUpdate", dto);
        }

        public async Task SendClickTimestampToAdmins(DateTime timestamp)
        {
            await Clients.Group(AdminGroup).SendAsync("ReceiveClickTimestamp", timestamp);
        }

        public async Task SendViewTimestampToAdmins(DateTime timestamp)
        {
            await Clients.Group(AdminGroup).SendAsync("ReceiveViewTimestamp", timestamp);
        }

        public async Task SendConversionTimestampToAdmins(DateTime timestamp)
        {
            await Clients.Group(AdminGroup).SendAsync("ReceiveConversionTimestamp", timestamp);
        }

        private List<string> AdTriggerToString(int triggers)
        {
            var l = new List<string>();
            foreach (var interaction in Enum.GetValues(typeof(InteractionType)))
            {
                if ((triggers & (int)interaction) != 0)
                    l.Add(interaction.ToString());
            }
            return l;
        }
    }
}