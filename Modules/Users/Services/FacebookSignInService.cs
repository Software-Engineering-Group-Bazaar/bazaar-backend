using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;

namespace YourAppNamespace.Users.Services
{
    public class FacebookSignInService
    {
        private readonly HttpClient _httpClient;

        public FacebookSignInService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<FacebookUserInfo?> ValidateFacebookTokenAsync(string accessToken)
        {
            var url = $"https://graph.facebook.com/me?fields=id,name,email&access_token={accessToken}";

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();

            var userInfo = JsonSerializer.Deserialize<FacebookUserInfo>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return userInfo;
        }
    }

    public class FacebookUserInfo
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
    }
}
