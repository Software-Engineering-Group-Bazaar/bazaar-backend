using System.Collections.Generic;
using System.Threading.Tasks;
using Order.DTOs;
using Order.Models;

namespace Order.Interface
{
    public interface IOrderService
    {
        Task<OrderModel> CreateOrderAsync(string buyerId, int storeId);

        Task<OrderModel?> GetOrderByIdAsync(int orderId);
        Task<IEnumerable<OrderModel>> GetAllOrdersAsync();
        Task<IEnumerable<OrderModel>> GetOrdersByBuyerAsync(string buyerId);
        Task<IEnumerable<OrderModel>> GetOrdersByStoreAsync(int storeId);
        Task<bool> UpdateOrderStatusAsync(int orderId, OrderStatus newStatus);
        Task<bool> DeleteOrderAsync(int orderId);
        Task<bool> UpdateOrderAsync(int id, string? buyerId, string? storeId, OrderStatus? status, DateTime? time, decimal? total);
    }
}