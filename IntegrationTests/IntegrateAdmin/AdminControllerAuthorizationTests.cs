/*//using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json; // For PostAsJsonAsync
//using System.IdentityModel.Tokens.Jwt; // Required for JWT
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using AdminApi.DTOs; // Your DTO namespace
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration; // Required for IConfiguration
using Microsoft.Extensions.DependencyInjection;
using Users.Models; // Your User model namespace
using Xunit;

namespace Tests.Integration; // Adjust namespace

// Assume TestContainerFixture manages your test DB container and provides the connection string
public class AdminControllerAuthorizationTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _anonymousClient;
    //private readonly TestContainerFixture _fixture; // Access fixture for seeding/config if needed

    // Roles used in tests
    private const string AdminRole = "Admin";
    private const string SellerRole = "Seller";
    private const string BuyerRole = "Buyer"; // Add other non-admin roles

    public AdminControllerAuthorizationTests()
    {
        // Pass the connection string from the fixture to the factory
        _factory = new CustomWebApplicationFactory();
        _anonymousClient = _factory.CreateClient();

        // --- Ensure roles exist (can also be done in fixture setup) ---
        InitializeRolesAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public void Dispose()
    {
        _factory?.Dispose();
        _anonymousClient?.Dispose();
        GC.SuppressFinalize(this);
    }

    // Helper to ensure roles exist once per test class run
    private async Task InitializeRolesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        string[] roleNames = { AdminRole, SellerRole, BuyerRole };
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }
    }

    // --- Authentication Helper ---
    private async Task<HttpClient> GetAuthenticatedClientAsync(string role)
    {
        var userManager = _factory.Services.GetRequiredService<UserManager<User>>();
        var configuration = _factory.Services.GetRequiredService<IConfiguration>();

        // Create or find a user for the role
        var uniqueSuffix = Guid.NewGuid().ToString("N").Substring(0, 6);
        var userName = $"{role}User_{uniqueSuffix}";
        var email = $"{userName}@test.example.com";
        var user = await userManager.FindByNameAsync(userName); // Less likely to exist with GUID

        if (user == null)
        {
            user = new User { UserName = userName, Email = email, EmailConfirmed = true, IsApproved = true };
            var createResult = await userManager.CreateAsync(user, "Password123!"); // Use a standard test password
            if (!createResult.Succeeded) throw new Exception($"Failed to create test user {userName}: {string.Join(",", createResult.Errors.Select(e => e.Description))}");

            var roleResult = await userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded) throw new Exception($"Failed to add user {userName} to role {role}: {string.Join(",", roleResult.Errors.Select(e => e.Description))}");

            // Refetch user to ensure all properties (like Id) are loaded
            user = await userManager.FindByNameAsync(userName);
            if (user == null) throw new Exception($"Failed to refetch created user {userName}");
        }

        // Generate JWT Token
        // var tokenHandler = new JwtSecurityTokenHandler();
        // var key = Encoding.ASCII.GetBytes(configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured in test environment"));
        // var claims = new List<Claim>
        // {
        //     new Claim(JwtRegisteredClaimNames.Sub, user.Id), // Subject (user ID)
        //     new Claim(JwtRegisteredClaimNames.Name, user.UserName),
        //     new Claim(JwtRegisteredClaimNames.Email, user.Email),
        //     new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // Unique token ID
        //     new Claim(ClaimTypes.Role, role) // Add the role claim
        // };

        // Add all roles the user actually has (good practice)
        // var actualRoles = await userManager.GetRolesAsync(user);
        // foreach(var actualRole in actualRoles) { claims.Add(new Claim(ClaimTypes.Role, actualRole)); }

        // var tokenDescriptor = new SecurityTokenDescriptor
        // {
        //     Subject = new ClaimsIdentity(claims),
        //     Expires = DateTime.UtcNow.AddMinutes(15), // Short expiry for tests
        //     Issuer = configuration["Jwt:Issuer"],
        //     Audience = configuration["Jwt:Audience"],
        //     SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        // };
        // var token = tokenHandler.CreateToken(tokenDescriptor);
        // var tokenString = tokenHandler.WriteToken(token);

        // Create HttpClient with Authorization header
        var client = _factory.CreateClient();
        //client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenString);
        return client;
    }

    // --- Test Methods ---

    [Theory]
    [InlineData("/api/admin/users")] // GET endpoint
    public async Task AdminEndpoints_ShouldReturnOk_ForAdminUser(string url)
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync(AdminRole);

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/api/admin/users")]                          // GET
    // Add other POST/PUT/DELETE endpoints that should succeed for Admin
    // Example (assuming valid DTO and data setup for POST/DELETE might be needed)
    // [InlineData("/api/admin/users/create")] // Needs POST handling
    // [InlineData("/api/admin/users/approve")] // Needs POST handling
    // [InlineData("/api/admin/user/some_existing_id")] // Needs DELETE handling
    public async Task AdminEndpoints_ShouldReturnForbidden_ForNonAdminUser(string url)
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync(SellerRole); // Test with a Seller

        // Act
        HttpResponseMessage response;
        // Adjust HTTP method based on URL if testing more than GET
        if (url.Contains("create") || url.Contains("approve"))
        {
            // For POST, send minimal valid data structure even if expecting Forbidden
            // Proper DTO might be needed depending on endpoint specifics
            var dummyDto = new { }; // Placeholder
            response = await client.PostAsJsonAsync(url, dummyDto);
        }
        else if (url.Contains("/api/admin/user/")) // Example DELETE
        {
            response = await client.DeleteAsync(url);
        }
        else // Default to GET
        {
            response = await client.GetAsync(url);
        }


        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.OK); // 403 Forbidden
    }

    [Theory]
    [InlineData("/api/admin/users")]                         // GET
    // Add other POST/PUT/DELETE endpoints
    // [InlineData("/api/admin/users/create")]
    // [InlineData("/api/admin/users/approve")]
    // [InlineData("/api/admin/user/some_id")]
    public async Task AdminEndpoints_ShouldReturnUnauthorized_ForAnonymousUser(string url)
    {
        // Arrange
        var client = _anonymousClient; // Use client without auth header

        // Act
        HttpResponseMessage response;
        // Adjust HTTP method based on URL if testing more than GET
        if (url.Contains("create") || url.Contains("approve"))
        {
            var dummyDto = new { };
            response = await client.PostAsJsonAsync(url, dummyDto);
        }
        else if (url.Contains("/api/admin/user/"))
        {
            response = await client.DeleteAsync(url);
        }
        else // Default to GET
        {
            response = await client.GetAsync(url);
        }

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.OK); // 401 Unauthorized
    }

    // --- Example: More Specific Test (Optional - if you want data checks too) ---
    [Fact]
    public async Task DeleteUser_AsAdmin_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync(AdminRole);
        var nonExistentUserId = "non-existent-user-id-123";

        // Act
        var response = await client.DeleteAsync($"/api/admin/user/{nonExistentUserId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound); // 404 Not Found
    }

    [Fact]
    public async Task CreateUser_AsAdmin_WithValidData_ShouldReturnCreated() // 201
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync(AdminRole);
        var uniqueSuffix = Guid.NewGuid().ToString("N").Substring(0, 8);
        var dto = new CreateUserDto
        {
            UserName = $"test_create_{uniqueSuffix}",
            Email = $"test_create_{uniqueSuffix}@example.com",
            Password = "ValidPassword123!"
            // Role is handled by controller internally in your example
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/admin/users/create", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created); // 201 Created

        // Optional: Verify user exists in DB afterwards
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var createdUser = await userManager.FindByNameAsync(dto.UserName);
        createdUser.Should().NotBeNull();
        var roles = await userManager.GetRolesAsync(createdUser);
        roles.Should().Contain(SellerRole); // Verify the role assigned by the controller

        // Cleanup (optional, depends on test DB strategy)
        // if (createdUser != null) await userManager.DeleteAsync(createdUser);
    }

    [Fact]
    public async Task ApproveUser_AsAdmin_WithUnapprovedUser_ShouldReturnOk()
    {
        // Arrange
        var client = await GetAuthenticatedClientAsync(AdminRole);

        // Create an unapproved user specifically for this test
        string userIdToApprove;
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var uniqueSuffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var user = new User { UserName = $"approve_{uniqueSuffix}", Email = $"approve_{uniqueSuffix}@test.com", EmailConfirmed = true, IsApproved = false };
            var result = await userManager.CreateAsync(user, "Password123!");
            result.Succeeded.Should().BeTrue("Failed to create user for approval test");
            userIdToApprove = user.Id;
        }

        var dto = new ApproveUserDto { UserId = userIdToApprove };

        // Act
        var response = await client.PostAsJsonAsync("/api/admin/users/approve", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify user is now approved in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var approvedUser = await userManager.FindByIdAsync(userIdToApprove);
            approvedUser.Should().NotBeNull();
            approvedUser.IsApproved.Should().BeTrue();
        }

        // Cleanup (optional)
        // using (var scope = _factory.Services.CreateScope()) { ... delete user ... }
    }
}
*/