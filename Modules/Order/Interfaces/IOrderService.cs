using System.Collections.Generic;
using System.Threading.Tasks;
using Order.DTOs;
using Order.Models;

namespace Order.Interface
{
    public interface IOrderService
    {
        Task<OrderModel> CreateOrderAsync(int buyerId, int storeId);

        Task<OrderModel?> GetOrderByIdAsync(int orderId);
        Task<IEnumerable<OrderModel>> GetAllOrdersAsync();
        Task<IEnumerable<OrderModel>> GetOrdersByBuyerAsync(int buyerId);
        Task<IEnumerable<OrderModel>> GetOrdersByStoreAsync(int storeId);
        Task<bool> UpdateOrderStatusAsync(int orderId, OrderStatus newStatus);
        Task<bool> DeleteOrderAsync(int orderId);
        Task<IEnumerable<OrderSummaryDto>> GetOrdersForSellerAsync(string sellerUserId);
        Task<bool> UpdateOrderStatusForSellerAsync(string sellerUserId, UpdateOrderStatusRequestDto updateDto);

    }
}