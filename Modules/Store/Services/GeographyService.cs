using Microsoft.EntityFrameworkCore;
using Store.Interface;
using Store.Models;

namespace Store.Services
{
    public class GeographyService : IGeographyService
    {
        private readonly StoreDbContext _context;

        public GeographyService(StoreDbContext context)
        {
            _context = context;
        }
        public async Task<Place> CreatePlaceAsync(Place place)
        {
            var region = await _context.Regions.FindAsync(place.RegionId);
            if (region is null)
            {
                throw new ArgumentException("Region not found.");
            }
            await _context.Places.AddAsync(place);
            await _context.SaveChangesAsync();
            return place;
        }

        public async Task<Region> CreateRegionAsync(Region region)
        {
            await _context.Regions.AddAsync(region);
            await _context.SaveChangesAsync();
            return region;
        }

        public async Task<bool> DeletePlaceAsync(int id)
        {
            var place = await _context.Places.FindAsync(id);
            if (place is null)
            {
                return false;
            }
            _context.Places.Remove(place);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteRegionAsync(int id)
        {
            var r = await _context.Regions.FindAsync(id);
            if (r is null)
            {
                return false;
            }
            _context.Regions.Remove(r);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<Place>> GetAllPlacesAsync()
        {
            var l = await _context.Places.Include(p => p.Region).ToListAsync();
            return l;
        }

        public async Task<IEnumerable<Place>> GetAllPlacesInRegionAsync(int regionId)
        {
            var l = await _context.Places.Include(p => p.Region).Where(p => p.RegionId == regionId).ToListAsync();
            return l;
        }

        public async Task<IEnumerable<Region>> GetAllRegionsAsync()
        {
            var l = await _context.Regions.ToListAsync();
            return l;
        }

        public async Task<Place?> GetPlaceByIdAsync(int id)
        {
            var p = await _context.Places.Include(p => p.Region).FirstOrDefaultAsync(p => p.Id == id);
            return p;
        }

        public async Task<Region?> GetRegionByIdAsync(int id)
        {
            var p = await _context.Regions.FirstOrDefaultAsync(p => p.Id == id);
            return p;
        }

        public Task<bool> UpdatePlaceAsync(Place place)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UpdateRegionAsync(Region region)
        {
            throw new NotImplementedException();
        }
    }
}