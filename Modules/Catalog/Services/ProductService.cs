using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Modules.Catalog.Models;
using Modules.Catalog.Services;
using Modules.Store.Models;
using S3Infrastrucutre.Interfaces;
using Store.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Modules.Catalog.Services
{
    public class ProductService : IProductService
    {
        private readonly IImageStorageService _imageStorageService;
        private readonly StoreDbContext _dbContext;

        public ProductService(IImageStorageService imageStorageService, StoreDbContext dbContext)
        {
            _imageStorageService = imageStorageService;
            _dbContext = dbContext;
        }

        public async Task<ProductDto?> AddProductToStoreAsync(
            string sellerUserId,
            int storeId,
            CreateProductRequestDto productData,
            List<IFormFile> imageFiles)
        {
            // 1. Provjera vlasništva nad prodavnicom
         /*   var store = await _dbContext.Stores
                //.FirstOrDefaultAsync(s => s.id == storeId && s.SellerUserId == sellerUserId);

            if (store == null)
                return null;*/

             //2. Validacija kategorije
      /*      var category = await _dbContext.StoreCategories
                .FirstOrDefaultAsync(c => c.Id == productData.ProductCategoryId);

            if (category == null)
                return null;*/

            // 3. Upload slika na S3
            var imageUrls = new List<string>();
            foreach (var file in imageFiles)
            {
                var imageUrl = await _imageStorageService.UploadImageAsync(file, "products");
                if (imageUrl != null)
                {
                    imageUrls.Add(imageUrl);
                }
            }

            // 4. Kreiranje Product entiteta
            var product = new Product
            {
                Name = productData.Name,
                ProductCategoryId = productData.ProductCategoryId,
                StoreId = storeId,
                Price = productData.Price,
                Weight = productData.Weight,
                WeightUnit = productData.WeightUnit,
                Volume = productData.Volume,
                VolumeUnit = productData.VolumeUnit,
                ProductImages = imageUrls.Select(url => new ProductImage
                {
                    ImageUrl = url
                }).ToList()
            };

            // 5. Čuvanje u bazu
            _dbContext.Products.Add(product);
            await _dbContext.SaveChangesAsync();

            // 6. Mapiranje u ProductDto
            var productDto = new ProductDto
            {
               Id = product.Id,
                Name = product.Name,
             /*  CategoryName = category.name,*/
                Price = product.Price,
                ImageUrls = product.ProductImages.Select(pi => pi.ImageUrl).ToList()
            };

            return productDto;
        }
    }
}
