using Users.Models;

namespace Users.Interfaces
{
    public interface IAddressService
    {
        Task<ICollection<Address>> GetUserAddressesAsync(string userId);
        Task<Address?> GetAddressByIdAsync(int id);
        Task CreateAddressAsync(Address address);
        Task DeleteAddressByIdAsync(int id);
    }
}