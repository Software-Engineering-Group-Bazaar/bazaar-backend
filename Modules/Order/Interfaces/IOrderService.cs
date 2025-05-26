using System.Collections.Generic;
using System.Threading.Tasks;
using Order.DTOs;
using Order.Models;

namespace Order.Interface
{
    public interface IOrderService
    {
        Task<OrderModel> CreateOrderAsync(string buyerId, int storeId, int addressId = 0);

        Task<OrderModel?> GetOrderByIdAsync(int orderId);
        Task<IEnumerable<OrderModel>> GetAllOrdersAsync();
        Task<IEnumerable<OrderModel>> GetOrdersByBuyerAsync(string buyerId);
        Task<IEnumerable<OrderModel>> GetOrdersByStoreAsync(int storeId);
        Task<bool> UpdateOrderStatusAsync(int orderId, OrderStatus newStatus, bool adminDelivery = false, int estimatedPreparationTimeInMinutes = 0);
        Task<bool> DeleteOrderAsync(int orderId);
        Task<bool> UpdateOrderAsync(int id, string? buyerId, int? storeId, OrderStatus? status, DateTime? time, decimal? total);
        Task SaveChange();
    }
}