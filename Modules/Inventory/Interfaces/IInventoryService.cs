using System.Collections.Generic;
using System.Threading.Tasks;
using Inventory.Dtos;

namespace Inventory.Interfaces
{
    public interface IInventoryService
    {
        Task<InventoryDto?> CreateInventoryAsync(string requestingUserId, bool isAdmin, CreateInventoryRequestDto createDto);

        Task<IEnumerable<InventoryDto>> GetInventoryAsync(string requestingUserId, bool isAdmin, int? productId = null, int? storeId = null);

        Task<InventoryDto?> UpdateQuantityAsync(string requestingUserId, bool isAdmin, UpdateInventoryQuantityRequestDto updateDto);

        Task<bool> DeleteInventoryAsync(string requestingUserId, bool isAdmin, int inventoryId);

    }

    public class UpdateInventoryQuantityRequestDto
    {
    }
}