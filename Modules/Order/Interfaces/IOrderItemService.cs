using System.Collections.Generic;
using System.Threading.Tasks;
using Order.Models;

namespace Order.Interface
{
    public interface IOrderItemService
    {
        Task<OrderItem> CreateOrderItemAsync(int orderId, int productId, int quantity);
        Task<bool> UpdateOrderItemAsync(int orderItemId, int quantity, int productId);
        Task<OrderItem?> GetOrderItemByIdAsync(int orderItemId);
        Task<IEnumerable<OrderItem>> GetOrderItemsByOrderIdAsync(int orderId);
        Task<bool> DeleteOrderItemAsync(int orderItemId);
    }
}