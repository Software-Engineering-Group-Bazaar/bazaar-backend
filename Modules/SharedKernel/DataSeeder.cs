using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Users.Models;


namespace SharedKernel
{
    public static class UserDataSeeder // Renamed for clarity
    {
        // Method to seed users - ensure Role Seeder runs first!
        public static async Task SeedDevelopmentUsersAsync(this IHost host)
        {
            using (var scope = host.Services.CreateScope())
            {
                var serviceProvider = scope.ServiceProvider;
                var environment = serviceProvider.GetRequiredService<IHostEnvironment>();

                // Only run seeding in the Development environment
                if (!environment.IsDevelopment())
                {
                    return;
                }

                // --- Get Required Services ---
                // Replace ApplicationUser with your actual user class inheriting IdentityUser
                var userManager = serviceProvider.GetRequiredService<UserManager<User>>();
                // Get RoleManager if you need to check roles exist, though it's better if the role seeder guarantees this
                // var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>(); // Or ILogger<IdentityDataSeeder>
                var usersContext = serviceProvider.GetRequiredService<UsersDbContext>(); // Still needed for MigrateAsync

                try
                {
                    logger.LogInformation("Applying migrations for Users database...");
                    await usersContext.Database.MigrateAsync(); // Ensure Identity tables are created/updated

                    // --- Define Roles (ensure these match roles created by your Role Seeder) ---
                    // These are the names of roles you expect to exist.
                    const string adminRole = "Admin";
                    const string storeOwnerRole = "Seller";
                    const string basicUserRole = "Buyer";

                    // You could optionally add checks here using roleManager.RoleExistsAsync() if needed,
                    // but it couples the seeders more tightly. Best practice is to ensure
                    // the Role Seeder runs first and successfully creates these roles.

                    logger.LogInformation("Seeding Development Users and assigning roles...");

                    // --- Seed Admin User ---
                    await CreateUserWithRolesAsync(
                        userManager,
                        logger,
                        email: "admin@bazaar.com",
                        password: "Pa55word!", // Use strong passwords even for dev, or load from config
                        roles: new[] { adminRole, basicUserRole } // Admin is also a User
                                                                  // Add custom properties if using ApplicationUser: , customPropertyValue: "value"
                        );

                    // --- Seed Store Owner User ---
                    await CreateUserWithRolesAsync(
                        userManager,
                        logger,
                        email: "seller@dev.local",
                        password: "Password123!",
                        roles: new[] { storeOwnerRole, basicUserRole }
                        // Add custom properties if using ApplicationUser: , storeId: null // Initially no store maybe
                        );

                    // --- Seed Basic User ---
                    await CreateUserWithRolesAsync(
                        userManager,
                        logger,
                        email: "buyer@dev.local",
                        password: "Password123!",
                        roles: new[] { basicUserRole }
                        );

                    logger.LogInformation("Development user seeding completed.");

                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while seeding the Identity database.");
                }
            }
        }

        // Helper method to create a user if they don't exist and assign roles
        private static async Task CreateUserWithRolesAsync(
            UserManager<User> userManager,
            ILogger logger,
            string email,
            string password,
            IEnumerable<string> roles
            // Add parameters for custom properties if needed: , string customPropertyValue, int? storeId = null
            )
        {
            // Check if user already exists
            if (await userManager.FindByEmailAsync(email) == null)
            {
                logger.LogInformation("Creating user: {Email}", email);

                // Replace IdentityUser with your specific TUser type (e.g., ApplicationUser) if needed
                var user = new User
                {
                    UserName = email, // Often use email as username for simplicity
                    Email = email,
                    EmailConfirmed = true, // Useful for development to bypass email confirmation
                    IsApproved = true
                    // Assign custom properties if using a derived class:
                    // CustomProperty = customPropertyValue,
                    // StoreId = storeId
                };

                // Create the user with password
                var result = await userManager.CreateAsync(user, password);

                if (result.Succeeded)
                {
                    logger.LogInformation("User {Email} created successfully.", email);

                    // Assign roles to the user
                    // Ensure the roles actually exist (should be guaranteed by the Role Seeder)
                    var rolesResult = await userManager.AddToRolesAsync(user, roles);
                    if (rolesResult.Succeeded)
                    {
                        logger.LogInformation("Assigned roles [{Roles}] to user {Email}.", string.Join(", ", roles), email);
                    }
                    else
                    {
                        // Log role assignment errors
                        logger.LogWarning("Failed to assign roles [{Roles}] to user {Email}. Errors: {Errors}",
                            string.Join(", ", roles), email, string.Join(", ", rolesResult.Errors.Select(e => e.Description)));
                    }
                }
                else
                {
                    // Log user creation errors
                    logger.LogError("Failed to create user {Email}. Errors: {Errors}", email, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                logger.LogInformation("User {Email} already exists. Skipping creation.", email);
                // Optional: You could add logic here to *ensure* the existing user has the correct roles,
                // but that adds complexity (check current roles, add missing ones). For simple seeding,
                // skipping is often sufficient.
            }
        }
    }
}