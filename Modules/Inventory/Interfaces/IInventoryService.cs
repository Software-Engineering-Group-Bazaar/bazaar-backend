using System.Collections.Generic;
using System.Threading.Tasks;
using Inventory.Dtos;

namespace Inventory.Interfaces
{
    public interface IInventoryService
    {
        // Metoda za kreiranje novog zapisa
        Task<InventoryDto?> CreateInventoryAsync(string requestingUserId, bool isAdmin, CreateInventoryRequestDto createDto);

        // ➤➤➤ DODAJ I OSTALE METODE ODMAH KAO PLACEHOLDERE (lakše je kasnije) ➤➤➤

        // Metoda za dohvat (iz #SELLER-BE-01-S5)
        Task<IEnumerable<InventoryDto>> GetInventoryAsync(string requestingUserId, bool isAdmin, int? productId = null, int? storeId = null);

        // Metoda za ažuriranje količine (iz #SELLER-BE-03-S5)
        Task<InventoryDto?> UpdateQuantityAsync(string requestingUserId, bool isAdmin, UpdateInventoryQuantityRequestDto updateDto);

        // Metoda za brisanje (iz #SELLER-BE-04-S5)
        Task<bool> DeleteInventoryAsync(string requestingUserId, bool isAdmin, int inventoryId);

    }
}