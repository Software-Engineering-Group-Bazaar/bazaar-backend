using System;
using System.IO;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Catalog.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Catalog.Services
{
    public class S3ImageStorageService : IImageStorageService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly IConfiguration _configuration;
        private readonly ILogger<S3ImageStorageService> _logger;
        private readonly string? _bucketName;
        private readonly string _region; // Dodajemo region

        public S3ImageStorageService(IAmazonS3 s3Client, IConfiguration configuration, ILogger<S3ImageStorageService> logger)
        {
            _s3Client = s3Client;
            _configuration = configuration;
            _logger = logger;
            // Pročitaj ime bucketa i region iz konfiguracije
            _bucketName = _configuration["S3Settings:BucketName"];
            // Pročitaj region iz AWS podešavanja ili specifično za S3
            _region = _configuration["AWS:Region"] ?? _s3Client.Config.RegionEndpoint?.SystemName ?? "eu-north-1"; // Fallback

            if (string.IsNullOrEmpty(_bucketName))
            {
                _logger.LogError("S3 BucketName nije konfigurisan u appsettings.");
                // Razmisli o bacanju izuzetka ovdje ako je bucket obavezan
            }
        }

        public async Task<string?> UploadImageAsync(IFormFile imageFile, string? subfolder = null)
        {
            if (imageFile == null || imageFile.Length == 0 || string.IsNullOrEmpty(_bucketName))
            {
                _logger.LogWarning("Upload fajl je prazan ili S3 bucket nije konfigurisan.");
                return null;
            }

            // Generiši jedinstveno ime fajla da izbjegneš prepisivanje
            var fileExtension = Path.GetExtension(imageFile.FileName);
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";

            // Kreiraj ključ (putanju unutar bucketa), uključujući opcioni subfolder
            var key = string.IsNullOrEmpty(subfolder) ? uniqueFileName : $"{subfolder.Trim('/')}/{uniqueFileName}";

            try
            {
                using var stream = imageFile.OpenReadStream();

                var putRequest = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key,
                    InputStream = stream,
                    ContentType = imageFile.ContentType,
                    // CannedACL = S3CannedACL.PublicRead // KORISTI SAMO AKO NISI podesio Bucket Policy za javno čitanje!
                    // Pošto JESMO podesili Bucket Policy, ovo nam NE TREBA.
                };

                _logger.LogInformation("Uploadujem fajl {FileName} na S3 bucket {BucketName} sa ključem {Key}", imageFile.FileName, _bucketName, key);

                var response = await _s3Client.PutObjectAsync(putRequest);

                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    // Konstruiši javni URL (pazi na format URL-a za tvoj region)
                    // Standardni format: https://{bucket-name}.s3.{region}.amazonaws.com/{key}
                    // Alternativni (virtual-hosted): https://s3.{region}.amazonaws.com/{bucket-name}/{key}
                    // Koristimo standardni
                    var imageUrl = $"https://{_bucketName}.s3.{_region}.amazonaws.com/{key}";
                    _logger.LogInformation("Fajl uspješno uploadovan. URL: {ImageUrl}", imageUrl);
                    return imageUrl;
                }
                else
                {
                    _logger.LogError("Greška pri uploadu na S3. Status kod: {StatusCode}", response.HttpStatusCode);
                    return null;
                }
            }
            catch (AmazonS3Exception e)
            {
                _logger.LogError(e, "AWS S3 Greška pri uploadu fajla {FileName}. Poruka: {Message}", imageFile.FileName, e.Message);
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Neočekivana greška pri uploadu fajla {FileName}. Poruka: {Message}", imageFile.FileName, e.Message);
                return null;
            }
        }
    }
}