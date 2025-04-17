using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Catalog.Models;
using Catalog.Services;
using Catalog.Interfaces;

namespace UnitTests.Services
{
    public class ProductServiceTests
    {
        private CatalogDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<CatalogDbContext>()
                .UseInMemoryDatabase("TestDb_" + System.Guid.NewGuid()) // Unique name per test
                .Options;

            return new CatalogDbContext(options);
        }

        [Fact]
        public async Task UpdateProductAvailabilityAsync_Valid_ReturnsTrue()
        {
            var db = GetDbContext();
            db.Stores.Add(new Catalog.Models.Store { Id = 1, SellerUserId = "seller123" });
            db.Products.Add(new Product { Id = 1, StoreId = 1, IsAvailable = true });
            await db.SaveChangesAsync();

            var service = new ProductService(db, Mock.Of<IImageStorageService>(), Mock.Of<ILogger<ProductService>>());

            var result = await service.UpdateProductAvailabilityAsync("seller123", 1, false);

            Assert.True(result);
            var updated = await db.Products.FindAsync(1);
            Assert.False(updated.IsAvailable);
        }

        [Fact]
        public async Task UpdateProductAvailabilityAsync_ProductNotFound_ReturnsFalse()
        {
            var db = GetDbContext();
            db.Stores.Add(new Catalog.Models.Store { Id = 1, SellerUserId = "seller123" });
            await db.SaveChangesAsync();

            var service = new ProductService(db, Mock.Of<IImageStorageService>(), Mock.Of<ILogger<ProductService>>());

            var result = await service.UpdateProductAvailabilityAsync("seller123", 999, true);

            Assert.False(result);
        }

        [Fact]
        public async Task UpdateProductAvailabilityAsync_WrongStoreOwner_ReturnsFalse()
        {
            var db = GetDbContext();
            db.Stores.Add(new Catalog.Models.Store { Id = 1, SellerUserId = "seller123" });
            db.Products.Add(new Product { Id = 1, StoreId = 2, IsAvailable = true }); // wrong store
            await db.SaveChangesAsync();

            var service = new ProductService(db, Mock.Of<IImageStorageService>(), Mock.Of<ILogger<ProductService>>());

            var result = await service.UpdateProductAvailabilityAsync("seller123", 1, false);

            Assert.False(result);
        }
    }
}
