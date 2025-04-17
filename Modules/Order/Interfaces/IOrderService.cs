using System.Threading.Tasks;

namespace Order.Interfaces
{
    public interface IOrderService
    {
        Task<bool> UpdateOrderStatusAsync(string sellerUserId, int orderId, string newStatus);
    }
}