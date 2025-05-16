using Microsoft.EntityFrameworkCore;
using Users.Interfaces;
using Users.Models;

namespace Users.Services
{
    public class AddressService : IAddressService
    {
        private readonly UsersDbContext _context;

        public AddressService(UsersDbContext context)
        {
            _context = context;
        }
        public async Task<ICollection<Address>> GetUserAddressesAsync(string userId)
        {
            return await _context.Addresses
                .Where(a => a.UserId == userId)
                .AsNoTracking()
                .ToListAsync();
        }
        public async Task<Address?> GetAddressByIdAsync(int id)
        {
            return await _context.Addresses
                .FindAsync(id);
        }
        public async Task CreateAddressAsync(Address address)
        {
            if (string.IsNullOrEmpty(address.UserId))
            {
                throw new InvalidDataException("No UserId specified in the address");
            }
            var user = await _context.Users.FindAsync(address.UserId);
            if (user == null)
            {
                throw new InvalidDataException($"User with UserId '{address.UserId}' not found.");
            }
            await _context.AddAsync(address);
            await _context.SaveChangesAsync();
        }
        public async Task DeleteAddressByIdAsync(int id)
        {
            var addressToDelete = await _context.Addresses.FindAsync(id);

            if (addressToDelete == null)
            {
                throw new KeyNotFoundException($"Address with ID {id} not found and cannot be deleted.");
            }

            _context.Addresses.Remove(addressToDelete);
            await _context.SaveChangesAsync();
        }
    }
}