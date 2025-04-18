using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Store.Models; // Namespace for Region, Place
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
                        roles: new[] { adminRole } // Admin is also a User
                                                   // Add custom properties if using ApplicationUser: , customPropertyValue: "value"
                        );

                    await CreateUserWithRolesAsync(
                       userManager,
                       logger,
                       email: "administrator@bazaar.com",
                       password: "Pa55word!", // Use strong passwords even for dev, or load from config
                       roles: new[] { adminRole } // Admin is also a User
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

    public static class GeographyDataSeeder
    {
        // Method to seed Regions and Places
        public static async Task SeedGeographyAsync(this IHost host)
        {
            using (var scope = host.Services.CreateScope())
            {
                var serviceProvider = scope.ServiceProvider;
                var environment = serviceProvider.GetRequiredService<IHostEnvironment>();

                // Only run seeding in the Development environment (or adjust as needed)
                if (!environment.IsDevelopment())
                {
                    // Or perhaps you want this data in all environments?
                    // If so, remove this check.
                    // return;
                }

                // --- Get Required Services ---
                var storeContext = serviceProvider.GetRequiredService<StoreDbContext>(); // Use your actual DbContext
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>(); // Logger specific to this seeder

                try
                {
                    logger.LogInformation("Applying migrations for Store database (for Geography)...");
                    // Important: Ensure the Store database schema is created/updated BEFORE seeding
                    await storeContext.Database.MigrateAsync();

                    logger.LogInformation("Seeding Regions (Kantoni) and Places (Gradovi/Općine)...");

                    // --- Seed Regions ---
                    // Use a helper to avoid duplicate code and check existence
                    var sarajevoRegion = await SeedRegionAsync(storeContext, logger, "Kanton Sarajevo");
                    var tuzlaRegion = await SeedRegionAsync(storeContext, logger, "Tuzlanski kanton");
                    var zenicaRegion = await SeedRegionAsync(storeContext, logger, "Zeničko-dobojski kanton");
                    var hnkRegion = await SeedRegionAsync(storeContext, logger, "Hercegovačko-neretvanski kanton");
                    var uskRegion = await SeedRegionAsync(storeContext, logger, "Unsko-sanski kanton");
                    var sbkRegion = await SeedRegionAsync(storeContext, logger, "Srednjobosanski kanton");
                    // Add other regions as needed...
                    var brckoRegion = await SeedRegionAsync(storeContext, logger, "Brčko Distrikt"); // Treat Brcko as a region for simplicity
                    var banjRegion = await SeedRegionAsync(storeContext, logger, "Banjalučka regija");

                    // Save regions before seeding places that depend on them
                    await storeContext.SaveChangesAsync(); // Save Changes after adding Regions

                    // --- Seed Places ---
                    // Seed places, associating them with the correct region ID
                    if (sarajevoRegion != null)
                    {
                        await SeedPlaceAsync(storeContext, logger, "Sarajevo - Centar", sarajevoRegion.Id, "71000");
                        await SeedPlaceAsync(storeContext, logger, "Ilidža", sarajevoRegion.Id, "71210");
                        await SeedPlaceAsync(storeContext, logger, "Vogošća", sarajevoRegion.Id, "71320");
                    }

                    if (tuzlaRegion != null)
                    {
                        await SeedPlaceAsync(storeContext, logger, "Tuzla", tuzlaRegion.Id, "75000");
                        await SeedPlaceAsync(storeContext, logger, "Lukavac", tuzlaRegion.Id, "75300");
                        await SeedPlaceAsync(storeContext, logger, "Gračanica", tuzlaRegion.Id, "75320");
                        await SeedPlaceAsync(storeContext, logger, "Živinice", tuzlaRegion.Id, "75270");
                    }

                    if (zenicaRegion != null)
                    {
                        await SeedPlaceAsync(storeContext, logger, "Zenica", zenicaRegion.Id, "72000");
                        await SeedPlaceAsync(storeContext, logger, "Kakanj", zenicaRegion.Id, "72240");
                        await SeedPlaceAsync(storeContext, logger, "Visoko", zenicaRegion.Id, "71300");
                        await SeedPlaceAsync(storeContext, logger, "Tešanj", zenicaRegion.Id, "74260");
                    }

                    if (hnkRegion != null)
                    {
                        await SeedPlaceAsync(storeContext, logger, "Mostar", hnkRegion.Id, "88000");
                        await SeedPlaceAsync(storeContext, logger, "Čapljina", hnkRegion.Id, "88300");
                        await SeedPlaceAsync(storeContext, logger, "Konjic", hnkRegion.Id, "88400");
                    }

                    if (uskRegion != null)
                    {
                        await SeedPlaceAsync(storeContext, logger, "Bihać", uskRegion.Id, "77000");
                        await SeedPlaceAsync(storeContext, logger, "Cazin", uskRegion.Id, "77220");
                        await SeedPlaceAsync(storeContext, logger, "Sanski Most", uskRegion.Id, "79260");
                    }

                    if (sbkRegion != null)
                    {
                        await SeedPlaceAsync(storeContext, logger, "Travnik", sbkRegion.Id, "72270");
                        await SeedPlaceAsync(storeContext, logger, "Vitez", sbkRegion.Id, "72250");
                        await SeedPlaceAsync(storeContext, logger, "Jajce", sbkRegion.Id, "70101");
                    }

                    if (brckoRegion != null)
                    {
                        await SeedPlaceAsync(storeContext, logger, "Brčko", brckoRegion.Id, "76100");
                    }

                    if (banjRegion != null)
                    {
                        await SeedPlaceAsync(storeContext, logger, "Banja Luka", banjRegion.Id, "78000");
                    }

                    // Add other places...

                    // Save places
                    await storeContext.SaveChangesAsync(); // Save Changes after adding Places

                    logger.LogInformation("Geography seeding completed.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while seeding the Geography data.");
                }
            }
        }

        // Helper method to seed a Region if it doesn't exist
        private static async Task<Region?> SeedRegionAsync(StoreDbContext context, ILogger logger, string regionName, string country = "ba")
        {
            // Check if region already exists (case-insensitive check recommended)
            var existingRegion = await context.Regions
                                           .FirstOrDefaultAsync(r => r.Name.ToLower() == regionName.ToLower());

            if (existingRegion == null)
            {
                logger.LogInformation("Creating Region: {RegionName}", regionName);
                var region = new Region
                {
                    Name = regionName,
                    Country = country
                };
                await context.Regions.AddAsync(region);
                // Note: SaveChangesAsync() is called later in the main method after adding potentially multiple regions
                return region; // Return the newly created region (without ID yet)
            }
            else
            {
                logger.LogInformation("Region {RegionName} already exists. Skipping creation.", regionName);
                return existingRegion; // Return the existing region (with ID)
            }
        }

        // Helper method to seed a Place if it doesn't exist
        private static async Task SeedPlaceAsync(StoreDbContext context, ILogger logger, string placeName, int regionId, string postalCode)
        {
            // Check if place already exists within the specific region (case-insensitive check recommended)
            var existingPlace = await context.Places
                                          .FirstOrDefaultAsync(p => p.Name.ToLower() == placeName.ToLower() && p.RegionId == regionId);

            if (existingPlace == null)
            {
                // We already ensured the region exists and have its ID
                logger.LogInformation("Creating Place: {PlaceName} in RegionId {RegionId}", placeName, regionId);
                var place = new Place
                {
                    Name = placeName,
                    RegionId = regionId,
                    PostalCode = postalCode
                };
                await context.Places.AddAsync(place);
                // Note: SaveChangesAsync() is called later in the main method
            }
            else
            {
                logger.LogInformation("Place {PlaceName} in RegionId {RegionId} already exists. Skipping creation.", placeName, regionId);
            }
        }
    }
}
