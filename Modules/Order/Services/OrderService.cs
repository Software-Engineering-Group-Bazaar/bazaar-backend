using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Order.Interfaces;
using Order.Models;
using Users.Models;

namespace Order.Services
{
    public class OrderService : IOrderService
    {
        private readonly UsersDbContext _userDbContext;
        private readonly OrderDbContext _orderDbContext;

        public OrderService(UsersDbContext userDbContext, OrderDbContext orderDbContext)
        {
            _userDbContext = userDbContext;
            _orderDbContext = orderDbContext;
        }

        public async Task<bool> UpdateOrderStatusAsync(string sellerUserId, int orderId, string newStatus)
        {
            // 1. Nađi korisnika i njegov StoreId
            var user = await _userDbContext.Users.FirstOrDefaultAsync(u => u.Id == sellerUserId);
            if (user == null || user.StoreId == null)
                return false;

            var storeId = user.StoreId.Value;

            // 2. Nađi narudžbu
            var order = await _orderDbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null || order.StoreId != storeId)
                return false;

            // 3. Validacija novog statusa
            if (!Enum.TryParse(typeof(OrderStatus), newStatus, true, out var parsedStatus))
                return false;

            var currentStatus = Enum.Parse<OrderStatus>(order.Status);
            var newParsedStatus = (OrderStatus)parsedStatus;

            var validTransitions = new Dictionary<OrderStatus, List<OrderStatus>>
            {
                { OrderStatus.Pending, new List<OrderStatus> { OrderStatus.Confirmed, OrderStatus.Cancelled } },
                { OrderStatus.Confirmed, new List<OrderStatus> { OrderStatus.Shipped } },
                { OrderStatus.Shipped, new List<OrderStatus> { OrderStatus.Delivered } }
            };

            if (validTransitions.ContainsKey(currentStatus) &&
                !validTransitions[currentStatus].Contains(newParsedStatus))
                return false;

            // 4. Ažuriraj status
            order.Status = newParsedStatus.ToString();
            _orderDbContext.Orders.Update(order);
            await _orderDbContext.SaveChangesAsync();

            return true;
        }
    }
}