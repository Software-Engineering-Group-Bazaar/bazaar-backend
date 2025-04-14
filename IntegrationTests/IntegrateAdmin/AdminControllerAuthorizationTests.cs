using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt; // <<--- DODATO
using System.Linq; // <<--- DODATO (za Select u Error porukama)
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using AdminApi.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens; // <<--- DODATO
using Users.Models;
using Xunit;


namespace Tests.Integration
{
    public class AdminControllerAuthorizationTests : IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _anonymousClient;

        private const string AdminRole = "Admin";
        private const string SellerRole = "Seller";
        private const string BuyerRole = "Buyer";

        public AdminControllerAuthorizationTests()
        {
            _factory = new CustomWebApplicationFactory();
            _anonymousClient = _factory.CreateClient();
            InitializeRolesAsync().GetAwaiter().GetResult();
        }

        // Dispose metoda ostaje ista...
        [Fact]
        public void Dispose()
        {
            _factory?.Dispose();
            _anonymousClient?.Dispose();
            GC.SuppressFinalize(this);
        }


        // InitializeRolesAsync metoda ostaje ista...
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


        // U AdminControllerAuthorizationTests.cs

        // --- Authentication Helper - POPRAVLJENO za COOKIE ---
        private async Task<HttpClient> GetAuthenticatedClientAsync(string role)
        {
            // --- Deo za kreiranje korisnika i generisanje tokena ostaje ISTI ---
            using var scope = _factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            // ... (kod za kreiranje User-a i dodavanje Role) ...
            var uniqueSuffix = Guid.NewGuid().ToString("N").Substring(0, 6);
            var userName = $"{role}User_{uniqueSuffix}";
            var email = $"{userName}@test.example.com";
            var user = await userManager.FindByNameAsync(userName);

            if (user == null)
            {
                user = new User { UserName = userName, Email = email, EmailConfirmed = true, IsApproved = true };
                var createResult = await userManager.CreateAsync(user, "Password123!");
                if (!createResult.Succeeded) throw new Exception($"Failed to create test user {userName}: {string.Join(",", createResult.Errors.Select(e => e.Description))}");

                var roleResult = await userManager.AddToRoleAsync(user, role);
                if (!roleResult.Succeeded) throw new Exception($"Failed to add user {userName} to role {role}: {string.Join(",", roleResult.Errors.Select(e => e.Description))}");

                user = await userManager.FindByNameAsync(userName);
                if (user == null) throw new Exception($"Failed to refetch created user {userName}");
            }


            var tokenHandler = new JwtSecurityTokenHandler();
            var secretKey = configuration["JwtSettings:SecretKey"];
            var issuer = configuration["JwtSettings:Issuer"];
            var audience = configuration["JwtSettings:Audience"];
            var expiryMinutes = configuration.GetValue<int>("JwtSettings:ExpiryMinutes", 15);

            if (string.IsNullOrEmpty(secretKey) || string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(audience))
            {
                var envKey = Environment.GetEnvironmentVariable("JwtSettings__SecretKey");
                var envIssuer = Environment.GetEnvironmentVariable("JwtSettings__Issuer");
                var envAudience = Environment.GetEnvironmentVariable("JwtSettings__Audience");
                throw new InvalidOperationException($"JWT settings missing in test environment. Key found: {!string.IsNullOrEmpty(secretKey)}. Issuer found: {!string.IsNullOrEmpty(issuer)}. Audience found: {!string.IsNullOrEmpty(audience)}. EnvKey: {envKey != null}. EnvIssuer: {envIssuer != null}. EnvAudience: {envAudience != null}");
            }
            var keyBytes = Encoding.UTF8.GetBytes(secretKey);
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Name, user.UserName),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };
            var actualRoles = await userManager.GetRolesAsync(user);
            foreach (var actualRole in actualRoles) { claims.Add(new Claim(ClaimTypes.Role, actualRole)); }
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(expiryMinutes),
                Issuer = issuer,
                Audience = audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);
            // --- Kraj generisanja tokena ---


            // --- Kreiranje Klijenta i POSTAVLJANJE COOKIE-JA ---
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                // Omogućava upravljanje cookie-jima za ovaj klijent
                AllowAutoRedirect = false, // Obično dobro za API testove
                HandleCookies = true
            });

            // Dodaj cookie u CookieContainer klijenta
            // Treba nam BaseAddress da bismo postavili cookie za odgovarajući domen
            // WebApplicationFactory obično koristi http://localhost
            var baseAddress = client.BaseAddress ?? new Uri("http://localhost");
            client.DefaultRequestHeaders.Host = baseAddress.Host; // Može pomoći

            var cookieContainer = new CookieContainer();
            cookieContainer.Add(baseAddress, new Cookie("X-Access-Token", tokenString, "/", baseAddress.Host));

            // Nažalost, direktno postavljanje CookieContainer-a na HttpClient iz WebApplicationFactory
            // nije uvek jednostavno ili podržano na svim verzijama.
            // Alternativa je da se koristi DelegatingHandler ili da se cookie doda ručno pre SVAKOG zahteva.

            // >>> JEDNOSTAVNIJI PRISTUP: Postavi cookie kao default header (manje realno, ali može raditi) <<<
            // Iako nije tehnički isto kao pravi browser cookie, neki server-side kod može ovo pokupiti.
            // Vredi probati ako postavljanje pravog CookieContainer-a ne radi lako.
            // client.DefaultRequestHeaders.Add("Cookie", $"X-Access-Token={tokenString}");

            // >>> BOLJI PRISTUP (ako prethodni ne radi): Koristi HttpClientHandler <<<
            // Ovo zahteva malo više podešavanja CustomWebApplicationFactory ili kreiranja HttpClient-a ručno.

            // >>> NAJČEŠĆI PRISTUP za testiranje cookie-ja sa WebApplicationFactory: <<<
            // HttpClient kreiran sa HandleCookies = true bi *trebalo* da koristi interni CookieContainer.
            // Pokušajmo da dodamo cookie direktno u njega pre vraćanja klijenta.
            // Ovo NIJE standardni API HttpClient-a, ali vredi probati u kontekstu testiranja.
            // Nažalost, standardni HttpClient nema direktan pristup CookieContainer-u.
            // Ostaje nam da probamo sa 'Cookie' headerom ili da refaktorišemo kreiranje klijenta.

            // Hajde da probamo sa Default Headerom "Cookie", jer je najlakše implementirati:
            client.DefaultRequestHeaders.Remove("Cookie"); // Ukloni stari ako postoji
            client.DefaultRequestHeaders.Add("Cookie", new Cookie("X-Access-Token", tokenString).ToString());


            // Ukloni Authorization header jer API sada gleda cookie
            // client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenString); // <<--- UKLONJENO/ZAKOMENTARISANO

            return client;
        }

        // --- Test Metode ---
        // Ostatak test metoda ostaje isti...

        [Theory]
        [InlineData("/api/admin/users")] // GET endpoint
        public async Task AdminEndpoints_ShouldReturnOk_ForAdminUser(string url)
        {
            // Arrange
            var client = await GetAuthenticatedClientAsync(AdminRole); // Sad vraća autentifikovanog klijenta

            // Act
            var response = await client.GetAsync(url);

            // Assert
            // Sad bi trebalo da bude OK jer klijent ima Admin rolu i token
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Theory]
        [InlineData("/api/admin/users")] // GET
        public async Task AdminEndpoints_ShouldReturnForbidden_ForNonAdminUser(string url)
        {
            // Arrange
            var client = await GetAuthenticatedClientAsync(SellerRole); // Sad vraća autentifikovanog Seller-a

            // Act
            HttpResponseMessage response;
            // (Ostatak logike za GET/POST/DELETE ostaje isti)
            if (url.Contains("create") || url.Contains("approve"))
            {
                var dummyDto = new { };
                response = await client.PostAsJsonAsync(url, dummyDto);
            }
            else if (url.Contains("/api/admin/user/"))
            {
                response = await client.DeleteAsync(url);
            }
            else
            {
                response = await client.GetAsync(url);
            }

            // Assert
            // Sad bi trebalo da bude Forbidden jer Seller nema Admin rolu
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden); // <<--- Eksplicitno proveravamo Forbidden
        }

        [Theory]
        [InlineData("/api/admin/users")] // GET
        public async Task AdminEndpoints_ShouldReturnUnauthorized_ForAnonymousUser(string url)
        {
            // Arrange
            var client = _anonymousClient;

            // Act
            HttpResponseMessage response;
            // (Ostatak logike za GET/POST/DELETE ostaje isti)
            if (url.Contains("create") || url.Contains("approve"))
            {
                var dummyDto = new { };
                response = await client.PostAsJsonAsync(url, dummyDto);
            }
            else if (url.Contains("/api/admin/user/"))
            {
                response = await client.DeleteAsync(url);
            }
            else
            {
                response = await client.GetAsync(url);
            }


            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized); // <<--- Eksplicitno proveravamo Unauthorized
        }

        [Fact]
        public async Task DeleteUser_AsAdmin_WithNonExistentId_ShouldReturnNotFound()
        {
            // Arrange
            var client = await GetAuthenticatedClientAsync(AdminRole); // Sad je Admin
            var nonExistentUserId = "non-existent-user-id-123";

            // Act
            var response = await client.DeleteAsync($"/api/admin/user/{nonExistentUserId}");

            // Assert
            // Sad bi trebalo da stigne do kontrolera i vrati NotFound
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task CreateUser_AsAdmin_WithValidData_ShouldReturnCreated()
        {
            // Arrange
            var client = await GetAuthenticatedClientAsync(AdminRole); // Sad je Admin
            var uniqueSuffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var dto = new CreateUserDto
            {
                UserName = $"test_create_{uniqueSuffix}",
                Email = $"test_create_{uniqueSuffix}@example.com",
                Password = "ValidPassword123!"
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/admin/users/create", dto);

            // Assert
            // Sad bi trebalo da stigne do kontrolera i vrati Created
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            // Optional: Verify user exists in DB afterwards (ostaje isto)
            using var scope = _factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var createdUser = await userManager.FindByNameAsync(dto.UserName);
            createdUser.Should().NotBeNull();
            var roles = await userManager.GetRolesAsync(createdUser);
            // PROVERI KOJU ROLU KONTROLER DODAJE! Ako dodaje Seller, ovo je OK.
            roles.Should().Contain(SellerRole);

        }

        [Fact]
        public async Task ApproveUser_AsAdmin_WithUnapprovedUser_ShouldReturnOk()
        {
            // Arrange
            var client = await GetAuthenticatedClientAsync(AdminRole); // Sad je Admin

            // Create an unapproved user (ostaje isto)
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
            // Sad bi trebalo da stigne do kontrolera i vrati OK
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify user is now approved in DB (ostaje isto)
            using (var scope = _factory.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var approvedUser = await userManager.FindByIdAsync(userIdToApprove);
                approvedUser.Should().NotBeNull();
                approvedUser.IsApproved.Should().BeTrue();
            }
        }
    }
}