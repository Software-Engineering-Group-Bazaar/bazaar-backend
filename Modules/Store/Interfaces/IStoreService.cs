using System.Collections.Generic;
using System.Threading.Tasks;
using AdminApi.DTOs;
using Store.Models;

namespace Store.Interface
{
    public interface IStoreService
    {
        // Prima Seller ID i DTO za kreiranje, vraća DTO za prikaz ili null, asinhrono
        Task<StoreGetDto?> CreateStoreForSellerAsync(string sellerUserId, StoreCreateDto createDto);

        // Opciono: Metoda za Admina
        // Task<StoreGetDto?> CreateStoreForAdminAsync(StoreCreateDto createDto);

        // Vraća listu DTO-a za prikaz, asinhrono
        Task<IEnumerable<StoreGetDto>> GetAllStoresAsync();

        // Vraća DTO za prikaz ili null, asinhrono
        Task<StoreGetDto?> GetStoreByIdAsync(int id);

        // Prima ID, DTO za ažuriranje, ID korisnika i info o roli, vraća ažurirani DTO ili null, asinhrono
        Task<StoreGetDto?> UpdateStoreAsync(int id, StoreUpdateDto updateDto, string requestingUserId, bool isAdmin);

        // Prima ID, ID korisnika i info o roli, vraća bool (uspjeh/neuspjeh), asinhrono
        Task<bool> DeleteStoreAsync(int id, string requestingUserId, bool isAdmin);

        // Opciono: Asinhrona provjera postojanja
        // Task<bool> DoesStoreExistAsync(int id);
    }
}