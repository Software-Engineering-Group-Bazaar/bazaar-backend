using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Users.Dtos; // Assuming LoginResponseDto is here
using Users.Interfaces;
using Users.Models;
using Users.Models.Dtos; // Assuming FacebookUserInfo might be here if not separate

namespace Users.Services
{
    public class FacebookSignInService : IFacebookSignInService
    {
        private readonly HttpClient _httpClient;
        private readonly UserManager<User> _userManager;
        private readonly IJWTService _jwtService;
        private const string FacebookLoginProvider = "Facebook"; // Define provider name

        public FacebookSignInService(
            HttpClient httpClient,
            UserManager<User> userManager,
            IJWTService jwtService)
        {
            _httpClient = httpClient;
            _userManager = userManager;
            _jwtService = jwtService;
        }

        public async Task<FacebookResponseDto?> SignInAsync(string accessToken, string app)
        {
            var userInfo = await ValidateFacebookTokenAsync(accessToken);

            // Validate based on Facebook ID now
            if (userInfo == null || string.IsNullOrEmpty(userInfo.Id))
            {
                // Log error: Failed to validate token or get Facebook User ID
                return null;
            }

            // Try to find user by Facebook Login
            var user = await _userManager.FindByLoginAsync(FacebookLoginProvider, userInfo.Id);

            if (user == null)
            {

                var uniqueUserName = $"facebook_{userInfo.Id}";
                // Generate a placeholder email
                var placeholderEmail = $"{uniqueUserName}@placeholder.local"; // Or another placeholder domain

                user = new User
                {

                    UserName = uniqueUserName,
                    Email = placeholderEmail,
                    EmailConfirmed = false,
                    IsApproved = false,

                };

                var result = await _userManager.CreateAsync(user);
                if (!result.Succeeded)
                {
                    return null;
                }

                var loginInfo = new UserLoginInfo(FacebookLoginProvider, userInfo.Id, FacebookLoginProvider);
                var addLoginResult = await _userManager.AddLoginAsync(user, loginInfo);
                if (!addLoginResult.Succeeded)
                {
                    return null;
                }

                // Add roles
                if (app == "buyer")
                    await _userManager.AddToRoleAsync(user, Role.Buyer.ToString());
                else
                    await _userManager.AddToRoleAsync(user, Role.Seller.ToString());
                // } // uncomment if fallback logic above is used
            }

            // User found or created, generate token
            var roles = await _userManager.GetRolesAsync(user);
            var (token, _) = await _jwtService.GenerateTokenAsync(user, roles);

            return new FacebookResponseDto
            {
                Token = token
            };
        }

        private async Task<FacebookUserInfo?> ValidateFacebookTokenAsync(string accessToken)
        {
            // Still request email, it might be returned sometimes, but don't rely on it
            var url = $"https://graph.facebook.com/me?fields=id,name,email&access_token={accessToken}";

            try
            {
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    // Log errorContent for debugging
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();

                var userInfo = JsonSerializer.Deserialize<FacebookUserInfo>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (userInfo == null || string.IsNullOrEmpty(userInfo.Id))
                {
                    return null;
                }

                return userInfo;
            }
            catch (HttpRequestException ex)
            {
                // Log network error
                return null;
            }
            catch (JsonException ex)
            {
                // Log deserialization error
                return null;
            }
            catch (Exception ex) // Catch broader exceptions
            {
                // Log unexpected error
                return null;
            }
        }
    }

    // Keep this class definition as Facebook might still return email sometimes
    public class FacebookUserInfo
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; } // Can be null or empty
    }
}