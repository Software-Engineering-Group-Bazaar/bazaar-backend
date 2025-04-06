using System;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity; // For RoleManager seeding
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging; // For logging
using Users.Models; // Your DbContext namespace

namespace Tests.Integration; // Adjust namespace

public class CustomWebApplicationFactory : WebApplicationFactory<Program> // Assuming Program is public now
{
    // No longer needs connection string input

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing"); // Keep using the Testing environment

        builder.ConfigureServices(services =>
        {


            // 2. Add DbContext using the InMemory provider
            //    Use a unique name for each factory instance to ensure isolation between test runs if the factory is reused.
            //    Or use a static name if you WANT tests within the same run to share the same DB state (less common).
            string inMemoryDbName = $"InMemoryDbForTesting_{Guid.NewGuid()}";
            services.AddDbContext<UsersDbContext>(options => // Replace YourDbContext
            {
                options.UseInMemoryDatabase(inMemoryDbName);
            });

            // --- Ensure Database Schema (Optional but Recommended for Identity) ---
            // Build a temporary service provider to get the DbContext
            var sp = services.BuildServiceProvider();
            using (var scope = sp.CreateScope())
            {
                var scopedServices = scope.ServiceProvider;
                var db = scopedServices.GetRequiredService<UsersDbContext>(); // Replace YourDbContext
                var logger = scopedServices.GetRequiredService<ILogger<CustomWebApplicationFactory>>();
                var roleManager = scopedServices.GetRequiredService<RoleManager<IdentityRole>>(); // Get RoleManager

                try
                {
                    logger.LogInformation("Ensuring InMemory database '{DbName}' is created.", inMemoryDbName);
                    // EnsureCreated() creates the schema based on your model for InMemory
                    // Migrate() is NOT used for InMemory.
                    db.Database.EnsureCreated();
                    logger.LogInformation("InMemory database schema created.");

                    // --- Seed Essential Data (like Roles) ---
                    // This ensures roles exist for user creation in tests.
                    SeedRoles(roleManager).GetAwaiter().GetResult();
                    logger.LogInformation("Seeded essential roles.");

                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred creating or seeding the InMemory database.");
                    throw;
                }
            }
        });
    }

    // Helper method to seed roles (can be kept here or moved)
    private static async Task SeedRoles(RoleManager<IdentityRole> roleManager)
    {
        string[] roleNames = { "Admin", "Seller", "Buyer" }; // Match your roles
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }
    }
}