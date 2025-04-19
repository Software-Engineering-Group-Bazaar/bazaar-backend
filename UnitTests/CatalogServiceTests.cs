using Xunit;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Catalog.Models;
using Catalog.Dtos;
using Catalog.Services;
using Catalog.Interfaces;
using System.IO;
using System.Text;
using System.Linq;

namespace UnitTests
{
    public class CatalogServiceTests
    {
        private readonly Mock<IProductService> _productServiceMock;
        private readonly Mock<IProductCategoryService> _categoryServiceMock;
        private readonly Mock<IImageStorageService> _imageStorageServiceMock;

        public CatalogServiceTests()
        {
            _productServiceMock = new Mock<IProductService>();
            _categoryServiceMock = new Mock<IProductCategoryService>();
            _imageStorageServiceMock = new Mock<IImageStorageService>();
        }

        [Fact]
        public async Task GetAllCategories_ReturnsList()
        {
            // Arrange
            var fakeCategories = new List<ProductCategory>
            {
                new ProductCategory { Id = 1, Name = "Food" },
                new ProductCategory { Id = 2, Name = "Drinks" }
            };

            _categoryServiceMock.Setup(x => x.GetAllCategoriesAsync())
                .ReturnsAsync(fakeCategories);

            // Act
            var result = await _categoryServiceMock.Object.GetAllCategoriesAsync();

            // Assert
            Assert.Equal(2, result.Count());
            Assert.Contains(result, c => c.Name == "Food");
        }

        [Fact]
        public async Task CreateCategory_CreatesAndReturnsCategory()
        {
            var newCategory = new ProductCategory { Name = "NewCat" };

            _categoryServiceMock.Setup(x => x.CreateCategoryAsync(It.IsAny<ProductCategory>()))
                .ReturnsAsync((ProductCategory c) => c);

            var result = await _categoryServiceMock.Object.CreateCategoryAsync(newCategory);

            Assert.Equal("NewCat", result.Name);
        }

        [Fact]
        public async Task CreateProduct_WithFiles_ReturnsProduct()
        {
            var product = new Product
            {
                Name = "Test Product",
                ProductCategoryId = 1,
                RetailPrice = 100,
                WholesalePrice = 80,
                StoreId = 123
            };

            var fileMock = new Mock<IFormFile>();
            var content = "Fake image content";
            var fileName = "test.jpg";
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
            fileMock.Setup(_ => _.OpenReadStream()).Returns(ms);
            fileMock.Setup(_ => _.FileName).Returns(fileName);
            fileMock.Setup(_ => _.Length).Returns(ms.Length);

            var files = new List<IFormFile> { fileMock.Object };

            _productServiceMock.Setup(p => p.CreateProductAsync(product, files))
                .ReturnsAsync(product);

            var result = await _productServiceMock.Object.CreateProductAsync(product, files);

            Assert.Equal("Test Product", result.Name);
            Assert.Equal(123, result.StoreId);
        }

        [Fact]
        public async Task GetProductById_ReturnsProduct()
        {
            var product = new Product
            {
                Id = 1,
                Name = "TestProduct",
                StoreId = 1,
                ProductCategoryId = 1,
                RetailPrice = 50,
                WholesalePrice = 30
            };

            _productServiceMock.Setup(p => p.GetProductByIdAsync(1))
                .ReturnsAsync(product);

            var result = await _productServiceMock.Object.GetProductByIdAsync(1);

            Assert.NotNull(result);
            Assert.Equal("TestProduct", result?.Name);
        }

        [Fact]
        public async Task SearchProductsByName_ReturnsMatches()
        {
            var searchTerm = "Phone";
            var fakeProducts = new List<Product>
            {
                new Product { Name = "Phone X" },
                new Product { Name = "Phone Y" }
            };

            _productServiceMock.Setup(p => p.SearchProductsByNameAsync(searchTerm))
                .ReturnsAsync(fakeProducts);

            var result = await _productServiceMock.Object.SearchProductsByNameAsync(searchTerm);

            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task UploadImage_ReturnsImageUrl()
        {
            var fileMock = new Mock<IFormFile>();
            var content = "Fake image content";
            var fileName = "image.jpg";
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
            fileMock.Setup(_ => _.OpenReadStream()).Returns(ms);
            fileMock.Setup(_ => _.FileName).Returns(fileName);
            fileMock.Setup(_ => _.Length).Returns(ms.Length);

            _imageStorageServiceMock.Setup(s => s.UploadImageAsync(fileMock.Object, null))
                .ReturnsAsync("https://example.com/image.jpg");

            var result = await _imageStorageServiceMock.Object.UploadImageAsync(fileMock.Object);

            Assert.Equal("https://example.com/image.jpg", result);
        }
    }
}
