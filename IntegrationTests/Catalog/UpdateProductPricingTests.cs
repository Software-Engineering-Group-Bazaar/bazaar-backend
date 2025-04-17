/*using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using IntegrateAdmin;

namespace IntegrationTests
{
    public class UpdateProductPricingTests : IClassFixture<WebAppFactory>
    {
        private readonly HttpClient _client;

        public UpdateProductPricingTests(WebAppFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Seller_Can_Update_Product_Pricing()
        {
            // 👇 Zamijeni token stvarnim JWT-om ako ne koristiš stubovanje/autentikaciju
            var token = "eyJhbGciOi..."; // testni token za Seller rolu
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var request = new
            {
                productId = 1,
                retailPrice = 22.99,
                wholesaleThreshold = 5,
                wholesalePrice = 18.50
            };

            var response = await _client.PostAsJsonAsync("/api/catalog/products/prices", request);

            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            responseBody.Should().Contain("retailPrice");
        }
    }
}
*/