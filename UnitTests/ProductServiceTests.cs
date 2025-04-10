/*using Xunit;
using Moq;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Modules.Catalog.Models;
using Modules.Catalog.Services;
using S3Infrastrucutre.Interfaces;
namespace S3Infrastrucutre.Interfaces
{
    public interface IImageStorageService
    {
        Task<string?> UploadImageAsync(IFormFile file, string? subfolder = null);
    }
}

public class ProductServiceTests
{
    [Fact]
    public async Task AddProductToStoreAsync_ReturnsProductDto_WithImageUrls()
    {
        // Arrange
        var mockImageService = new Mock<IImageStorageService>();
        mockImageService
            .Setup(service => service.UploadImageAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
            .ReturnsAsync("https://mocked-s3-url.com/fake-image.jpg");

        var service = new ProductService(mockImageService.Object);

        var productData = new CreateProductRequestDto
        {
            Name = "Test proizvod",
            ProductCategoryId = 1,
            Price = 10.0m
        };

        // Simuliraj fajlove
        var image = new FormFile(new MemoryStream(new byte[256]), 0, 256, "image", "test.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };

        var result = await service.AddProductToStoreAsync("seller123", 1, productData, new List<IFormFile> { image });

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test proizvod", result.Name);
        Assert.Single(result.ImageUrls);
        Assert.StartsWith("https://mocked-s3-url.com", result.ImageUrls[0]);
    }
}
*/