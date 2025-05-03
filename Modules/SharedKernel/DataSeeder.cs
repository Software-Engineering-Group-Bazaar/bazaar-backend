using Catalog.Models;
using MarketingAnalytics.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Order.Models;
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

                    // --- Seed Additional Random Users ---
                    logger.LogInformation("Seeding additional random development users...");
                    int numberOfAdditionalUsers = 30;
                    string defaultPassword = "Password123!"; // Standard password for generated users

                    for (int i = 1; i <= numberOfAdditionalUsers; i++)
                    {
                        string email;
                        List<string> roles = new List<string> { basicUserRole }; // All are at least Buyers

                        // Make roughly 1 in 5 additional users also a Seller
                        if (i % 5 == 0)
                        {
                            email = $"seller{i}@dev.local";
                            roles.Add(storeOwnerRole); // Add Seller role
                        }
                        else
                        {
                            email = $"buyer{i}@dev.local";
                        }

                        await CreateUserWithRolesAsync(userManager, logger, email, defaultPassword, roles);
                    }

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

    public static class AdvertisementDataSeeder
    {
        // Method to seed Advertisements and AdData
        public static async Task SeedAdvertisementsAsync(this IHost host)
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
                var adDbContext = serviceProvider.GetRequiredService<AdDbContext>();
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>(); // Consistent logger type

                try
                {
                    logger.LogInformation("Applying migrations for Marketing Analytics database (for Advertisements)...");
                    await adDbContext.Database.MigrateAsync();

                    logger.LogInformation("Seeding Advertisements and AdData...");

                    // --- Define Seller IDs (should match users seeded by UserDataSeeder) ---
                    const string defaultSellerId = "e27254f1-f14d-4c79-9f43-d841b6f9767a";

                    // --- Seed Example Advertisements ---
                    // We retrieve the advertisement entity (or null if it exists)
                    // Then add AdData ONLY if the advertisement was newly created in this run.

                    // Example 1: Active Ad targeting a Specific Store
                    var ad1 = await SeedAdvertisementAsync(adDbContext, logger,
                        sellerId: defaultSellerId,
                        startTime: DateTime.UtcNow.AddDays(-5),
                        endTime: DateTime.UtcNow.AddDays(10),
                        views: 1500,
                        clicks: 75
                    );
                    if (ad1 != null) // Only add AdData if the Advertisment was newly created
                    {
                        await SeedAdDataAsync(adDbContext, logger, ad1, // Pass the parent entity
                            imageUrl: "https://via.placeholder.com/600x300/0000FF/FFFFFF?text=Store+Sale",
                            storeId: 1, productId: null, description: "Big Summer Sale at Store 1!"
                        );
                        await SeedAdDataAsync(adDbContext, logger, ad1,
                            imageUrl: "https://via.placeholder.com/300x300/FF0000/FFFFFF?text=Visit+Us",
                            storeId: 1, productId: null, description: "Don't miss out!"
                        );
                        // Add the parent (with its populated collection) to the context
                        await adDbContext.Advertisments.AddAsync(ad1);
                    }

                    // Example 2: Active Ad targeting Specific Products
                    var ad2 = await SeedAdvertisementAsync(adDbContext, logger,
                        sellerId: defaultSellerId,
                        startTime: DateTime.UtcNow.AddDays(-1),
                        endTime: DateTime.UtcNow.AddDays(30),
                        views: 850,
                        clicks: 120
                    );
                    if (ad2 != null)
                    {
                        await SeedAdDataAsync(adDbContext, logger, ad2,
                           imageUrl: "https://via.placeholder.com/400x400/008000/FFFFFF?text=Product+A",
                           storeId: null, productId: 101, description: "Get Product A - 20% Off"
                        );
                        await SeedAdDataAsync(adDbContext, logger, ad2,
                           imageUrl: "https://via.placeholder.com/400x400/FFA500/FFFFFF?text=Product+B",
                           storeId: null, productId: 102, description: "New Arrival: Product B"
                        );
                        await adDbContext.Advertisments.AddAsync(ad2);
                    }


                    // Example 3: Future Ad (Inactive) - General Purpose
                    var ad3 = await SeedAdvertisementAsync(adDbContext, logger,
                        sellerId: defaultSellerId,
                        startTime: DateTime.UtcNow.AddDays(15),
                        endTime: DateTime.UtcNow.AddDays(45),
                        views: 0,
                        clicks: 0
                    );
                    if (ad3 != null)
                    {
                        await SeedAdDataAsync(adDbContext, logger, ad3,
                           imageUrl: "https://via.placeholder.com/500x200/800080/FFFFFF?text=Coming+Soon",
                           storeId: null, productId: null, description: "Exciting announcement coming soon!"
                        );
                        await adDbContext.Advertisments.AddAsync(ad3);
                    }

                    // Example 4: Past Ad (Inactive) - Store and Product Mix
                    var ad4 = await SeedAdvertisementAsync(adDbContext, logger,
                        sellerId: defaultSellerId,
                        startTime: DateTime.UtcNow.AddDays(-60),
                        endTime: DateTime.UtcNow.AddDays(-30),
                        views: 12000,
                        clicks: 450
                    );
                    if (ad4 != null)
                    {
                        await SeedAdDataAsync(adDbContext, logger, ad4,
                           imageUrl: "https://via.placeholder.com/350x350/FFFF00/000000?text=Clearance",
                           storeId: 2, productId: 205, description: "Clearance Sale at Store 2 on Product 205!"
                        );
                        await SeedAdDataAsync(adDbContext, logger, ad4,
                           imageUrl: null, storeId: 2, productId: null, description: "Final days of our Store 2 event."
                        );
                        await adDbContext.Advertisments.AddAsync(ad4);
                    }


                    // Example 5: Ad with only description (Active)
                    var ad5 = await SeedAdvertisementAsync(adDbContext, logger,
                       sellerId: defaultSellerId,
                       startTime: DateTime.UtcNow.AddHours(-12),
                       endTime: DateTime.UtcNow.AddDays(5),
                       views: 300,
                       clicks: 5
                   );
                    if (ad5 != null)
                    {
                        await SeedAdDataAsync(adDbContext, logger, ad5,
                           imageUrl: null, storeId: null, productId: null, description: "Flash deal happening now! Limited time offer."
                        );
                        await adDbContext.Advertisments.AddAsync(ad5);
                    }


                    // --- Save all newly added advertisements and their ad data ---
                    var changes = await adDbContext.SaveChangesAsync();
                    if (changes > 0)
                    {
                        logger.LogInformation("Successfully saved {Count} new entities (Advertisements and related AdData) to the database.", changes);
                    }
                    else
                    {
                        logger.LogInformation("No new Advertisements or AdData needed seeding.");
                    }

                    logger.LogInformation("Advertisement seeding completed.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while seeding the Advertisement data.");
                }
            }
        }

        // Helper method to seed an Advertisement if it doesn't exist
        // Returns the NEWLY CREATED (unsaved) entity, or null if it already existed.
        private static async Task<Advertisment?> SeedAdvertisementAsync(
            AdDbContext context,
            ILogger logger,
            string sellerId,
            DateTime startTime,
            DateTime endTime,
            int views,
            int clicks)
        {
            // Check if an advertisement with the same SellerId and StartTime already exists.
            bool exists = await context.Advertisments
                                       .AsNoTracking() // Performance: No need to track if just checking existence
                                       .AnyAsync(a => a.SellerId == sellerId && a.StartTime == startTime);

            if (!exists)
            {
                logger.LogInformation("Creating Advertisement for Seller {SellerId} starting at {StartTime}.", sellerId, startTime);

                var advertisement = new Advertisment // Using the Advertisment MODEL
                {
                    SellerId = sellerId,
                    StartTime = startTime,
                    EndTime = endTime,
                    Views = views,
                    Clicks = clicks,
                    IsActive = DateTime.UtcNow >= startTime && DateTime.UtcNow < endTime,
                    // AdData collection is initially empty, will be populated by SeedAdDataAsync calls
                };
                // DO NOT add to context here. Return the transient object.
                return advertisement;
            }
            else
            {
                logger.LogInformation("Advertisement for Seller {SellerId} starting at {StartTime} already exists. Skipping creation.", sellerId, startTime);
                return null; // Indicate that it already existed
            }
        }

        // Helper method to create and add an AdData entity to its parent Advertisement's collection.
        // This assumes the parent Advertisement is a NEW, transient object passed from the main seeder logic.
        private static Task SeedAdDataAsync( // Task return type for consistency, though it's synchronous currently
            AdDbContext context, // Context potentially needed if complex checks were added later
            ILogger logger,
            Advertisment parentAdvertisement, // The parent MODEL object
            string? imageUrl,
            int? storeId,
            int? productId,
            string? description)
        {
            // Create the AdData MODEL instance
            var adData = new AdData
            {
                ImageUrl = imageUrl,
                StoreId = storeId,
                ProductId = productId,
                Description = description
                // DO NOT set AdvertismentId or Advertisment navigation property here.
                // EF Core handles this relationship when the parentAdvertisement
                // (which contains this adData in its collection) is added to the context.
            };

            // Add the new AdData entity TO THE PARENT'S COLLECTION.
            // This is crucial for EF Core to establish the relationship.
            parentAdvertisement.AdData.Add(adData);

            logger.LogDebug("Prepared AdData (Desc: {Description}) to be added to Advertisement for Seller {SellerId}",
                 description ?? "N/A", parentAdvertisement.SellerId);

            return Task.CompletedTask; // Return completed task as this helper is currently synchronous
        }
    }

    public static class StoreDataSeeder
    {
        // Method to seed Stores
        public static async Task SeedStoresAsync(this IHost host)
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
                var storeDbContext = serviceProvider.GetRequiredService<StoreDbContext>();
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>(); // Consistent logger type

                try
                {
                    logger.LogInformation("Applying migrations for Store database (for Stores)...");
                    await storeDbContext.Database.MigrateAsync();

                    logger.LogInformation("Seeding Store Categories and Stores...");

                    // --- 1. Ensure Store Categories Exist ---
                    var categoryGrocery = await SeedStoreCategoryAsync(storeDbContext, logger, "Prehrambene namirnice");
                    var categoryElectronics = await SeedStoreCategoryAsync(storeDbContext, logger, "Elektronika");
                    var categoryClothing = await SeedStoreCategoryAsync(storeDbContext, logger, "Odjeća i Obuća");
                    var categoryPharmacy = await SeedStoreCategoryAsync(storeDbContext, logger, "Apoteka");
                    var categoryHardware = await SeedStoreCategoryAsync(storeDbContext, logger, "Željezarija");
                    var categoryKiosk = await SeedStoreCategoryAsync(storeDbContext, logger, "Kiosk / Trafika");

                    await storeDbContext.SaveChangesAsync(); // Save categories first
                    logger.LogInformation("Store categories seeded/checked.");

                    // --- 2. Check Existing Store Count ---
                    const int targetStoreCount = 60; // Define the desired total number
                    var currentStoreCount = await storeDbContext.Stores.CountAsync();
                    logger.LogInformation("Current number of stores in DB: {CurrentCount}. Target: {TargetCount}", currentStoreCount, targetStoreCount);

                    int storesToCreate = targetStoreCount - currentStoreCount;

                    if (storesToCreate <= 0)
                    {
                        logger.LogInformation("Database already contains {CurrentCount} stores (target is {TargetCount}). No new stores will be seeded.", currentStoreCount, targetStoreCount);
                        return; // Exit early, we have enough or more stores
                    }

                    logger.LogInformation("Need to seed {StoresToCreate} additional Stores to reach the target of {TargetCount}...", storesToCreate, targetStoreCount);


                    // --- 3. Get Available Place and Category IDs (Only if needed) ---
                    var availablePlaceIds = await storeDbContext.Places.Select(p => p.Id).ToListAsync();
                    if (!availablePlaceIds.Any())
                    {
                        logger.LogWarning("No Places found in the database. GeographyDataSeeder might need to run first. Cannot seed Stores.");
                        return;
                    }

                    var availableCategoryIds = await storeDbContext.StoreCategories.Select(c => c.id).ToListAsync();
                    if (!availableCategoryIds.Any())
                    {
                        logger.LogWarning("No Store Categories found in the database. Cannot seed Stores.");
                        return;
                    }

                    // --- 4. Seed Required Number of Stores ---
                    var random = Random.Shared;

                    for (int i = 1; i <= storesToCreate; i++) // Loop only the required number of times
                    {
                        int randomPlaceId = availablePlaceIds[random.Next(availablePlaceIds.Count)];
                        int randomCategoryId = availableCategoryIds[random.Next(availableCategoryIds.Count)];

                        // Fetch names for better store naming (optional but nice)
                        var placeName = (await storeDbContext.Places.FindAsync(randomPlaceId))?.Name ?? $"Mjesto{randomPlaceId}";
                        var categoryName = (await storeDbContext.StoreCategories.FindAsync(randomCategoryId))?.name ?? $"Kat{randomCategoryId}";

                        // Generate unique store name - incorporate current count to avoid naming collisions across runs
                        string storeName = $"{categoryName} {placeName} # {currentStoreCount + i}"; // Makes names like "Elektronika Sarajevo #31"
                        string address = $"Ulica Trgovine {currentStoreCount + i}, {placeName}";
                        string description = $"Opis za prodavnicu {storeName}.";
                        bool isActive = random.Next(10) < 9;

                        await SeedStoreAsync(storeDbContext, logger,
                            name: storeName,
                            categoryId: randomCategoryId,
                            placeId: randomPlaceId,
                            address: address,
                            description: description,
                            isActive: isActive
                            );
                    }

                    // --- 5. Save Newly Added Stores ---
                    var changes = await storeDbContext.SaveChangesAsync();
                    if (changes > 0)
                    {
                        logger.LogInformation("Successfully saved {Count} new Store entities to the database.", changes);
                    }
                    else
                    {
                        logger.LogInformation("No new Stores needed seeding in this run (or SaveChanges failed).");
                    }

                    logger.LogInformation("Store seeding completed.");

                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while seeding the Store data.");
                }
            }
        }

        // Helper method to seed a Store Category if it doesn't exist by name
        // Returns the existing or newly created category ID.
        private static async Task<StoreCategory?> SeedStoreCategoryAsync(StoreDbContext context, ILogger logger, string categoryName)
        {
            // Case-insensitive check recommended
            var existingCategory = await context.StoreCategories
                                                .FirstOrDefaultAsync(c => c.name.ToLower() == categoryName.ToLower());

            if (existingCategory == null)
            {
                logger.LogInformation("Creating Store Category: {CategoryName}", categoryName);
                var category = new StoreCategory
                {
                    name = categoryName,
                    stores = new List<StoreModel>() // Ensure collection is initialized
                };
                await context.StoreCategories.AddAsync(category);
                // SaveChangesAsync is called later in the main method.
                return category; // Return the newly created, unsaved category
            }
            else
            {
                logger.LogInformation("Store Category {CategoryName} already exists. Skipping creation.", categoryName);
                return existingCategory; // Return the existing category
            }
        }


        // Helper method to seed a Store if it doesn't exist by name
        private static async Task SeedStoreAsync(
            StoreDbContext context,
            ILogger logger,
            string name,
            int categoryId,
            int placeId,
            string address,
            string? description,
            bool isActive)
        {
            bool exists = await context.Stores.AsNoTracking().AnyAsync(s => s.name == name);

            if (!exists)
            {
                var category = await context.StoreCategories.FindAsync(categoryId);
                var place = await context.Places.FindAsync(placeId);

                if (category == null) { logger.LogWarning("Category ID {CategoryId} not found for Store '{StoreName}'. Skipping.", categoryId, name); return; }
                if (place == null) { logger.LogWarning("Place ID {PlaceId} not found for Store '{StoreName}'. Skipping.", placeId, name); return; }
                // *** Generate random CreatedAt time ***
                DateTime randomCreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 366 * 10)).AddHours(-Random.Shared.Next(0, 24)); // Randomly within the last 10 years

                logger.LogInformation("Creating Store: {StoreName}", name);
                var store = new StoreModel
                {
                    name = name,
                    category = category,
                    place = place,
                    address = address,
                    description = description,
                    isActive = isActive,
                    createdAt = randomCreatedAt
                };
                await context.Stores.AddAsync(store);
                // SaveChangesAsync called later
            }
            else
            {
                logger.LogInformation("Store {StoreName} already exists. Skipping creation.", name);
            }
        }
    }

    public static class ProductDataSeeder
    {
        // Method to seed Products, matching them to Store Categories, limited per store
        public static async Task SeedProductsAsync(this IHost host)
        {
            using (var scope = host.Services.CreateScope())
            {
                var serviceProvider = scope.ServiceProvider;
                var environment = serviceProvider.GetRequiredService<IHostEnvironment>();

                if (!environment.IsDevelopment())
                {
                    return;
                }

                var catalogDbContext = serviceProvider.GetRequiredService<CatalogDbContext>();
                var storeDbContext = serviceProvider.GetRequiredService<StoreDbContext>();
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

                try
                {
                    logger.LogInformation("Applying migrations for Catalog database (for Products)...");
                    await catalogDbContext.Database.MigrateAsync();
                    logger.LogInformation("Applying migrations for Store database (needed for Store details)...");
                    await storeDbContext.Database.MigrateAsync();

                    logger.LogInformation("Seeding Product Categories and Products (limited per Store)...");

                    // --- 1. Ensure Product Categories Exist ---
                    // (Keep the comprehensive category seeding logic)
                    var categories = new Dictionary<string, ProductCategory>
                    { /* ... Same as before ... */
                        // Groceries
                        ["Voće"] = await SeedProductCategoryAsync(catalogDbContext, logger, "Voće"),
                        ["Povrće"] = await SeedProductCategoryAsync(catalogDbContext, logger, "Povrće"),
                        ["Mliječni proizvodi"] = await SeedProductCategoryAsync(catalogDbContext, logger, "Mliječni proizvodi"),
                        ["Meso i Mesne prerađevine"] = await SeedProductCategoryAsync(catalogDbContext, logger, "Meso i Mesne prerađevine"),
                        ["Pića"] = await SeedProductCategoryAsync(catalogDbContext, logger, "Pića"),
                        ["Slatkiši i Grickalice"] = await SeedProductCategoryAsync(catalogDbContext, logger, "Slatkiši i Grickalice"),
                        ["Pekarski proizvodi"] = await SeedProductCategoryAsync(catalogDbContext, logger, "Pekarski proizvodi"),
                        // Electronics
                        ["Računari i Oprema"] = await SeedProductCategoryAsync(catalogDbContext, logger, "Računari i Oprema"),
                        ["Mobiteli i Oprema"] = await SeedProductCategoryAsync(catalogDbContext, logger, "Mobiteli i Oprema"),
                        ["TV i Audio"] = await SeedProductCategoryAsync(catalogDbContext, logger, "TV i Audio"),
                        ["Mali Kućanski Aparati"] = await SeedProductCategoryAsync(catalogDbContext, logger, "Mali Kućanski Aparati"),
                        // Clothing & Shoes
                        ["Odjeća"] = await SeedProductCategoryAsync(catalogDbContext, logger, "Odjeća"),
                        ["Obuća"] = await SeedProductCategoryAsync(catalogDbContext, logger, "Obuća"),
                        // Pharmacy
                        ["Lijekovi bez recepta"] = await SeedProductCategoryAsync(catalogDbContext, logger, "Lijekovi bez recepta"),
                        ["Vitamini i Suplementi"] = await SeedProductCategoryAsync(catalogDbContext, logger, "Vitamini i Suplementi"),
                        ["Medicinska Kozmetika"] = await SeedProductCategoryAsync(catalogDbContext, logger, "Medicinska Kozmetika"),
                        ["Lična Higijena"] = await SeedProductCategoryAsync(catalogDbContext, logger, "Lična Higijena"),
                        // Hardware Store
                        ["Alati"] = await SeedProductCategoryAsync(catalogDbContext, logger, "Alati"),
                        ["Boje i Lakovi"] = await SeedProductCategoryAsync(catalogDbContext, logger, "Boje i Lakovi"),
                        ["Vrtni Program"] = await SeedProductCategoryAsync(catalogDbContext, logger, "Vrtni Program"),
                        // Kiosk
                        ["Novine i Časopisi"] = await SeedProductCategoryAsync(catalogDbContext, logger, "Novine i Časopisi"),
                        ["Cigarete i Duhan"] = await SeedProductCategoryAsync(catalogDbContext, logger, "Cigarete i Duhan"),
                        ["Dopune za mobitel"] = await SeedProductCategoryAsync(catalogDbContext, logger, "Dopune za mobitel")
                    };
                    categories = categories.Where(kvp => kvp.Value != null).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    await catalogDbContext.SaveChangesAsync();
                    logger.LogInformation("Product categories seeded/checked.");

                    // --- 2. Fetch Prerequisites ---
                    var productCategoriesLookup = await catalogDbContext.ProductCategories.AsNoTracking().ToDictionaryAsync(pc => pc.Name, pc => pc.Id);
                    var storeToProductCategoryMap = GetStoreToProductCategoryMap();
                    var storesToSeed = await storeDbContext.Stores.Include(s => s.category).Where(s => s.category != null).ToListAsync();

                    // *** Pre-fetch existing product counts per store for efficiency ***
                    var productCountsByStoreId = await catalogDbContext.Products
                                                    .GroupBy(p => p.StoreId)
                                                    .Select(g => new { StoreId = g.Key, Count = g.Count() })
                                                    .ToDictionaryAsync(x => x.StoreId, x => x.Count);

                    if (!storesToSeed.Any()) { logger.LogWarning("No Stores with Categories found. Cannot seed Products."); return; }
                    if (!productCategoriesLookup.Any()) { logger.LogWarning("No Product Categories found. Cannot seed Products."); return; }

                    // --- 3. Seed Products Per Store (Limited) ---
                    logger.LogInformation("Seeding Products per Store...");
                    const int targetProductsPerStore = 5; // <<<--- TARGET PER STORE (Adjust as needed, e.g., 2-5)
                    var random = Random.Shared;
                    int productNamingCounter = await catalogDbContext.Products.CountAsync(); // Start naming counter from existing total

                    foreach (var store in storesToSeed)
                    {
                        // Get current count for *this* store
                        productCountsByStoreId.TryGetValue(store.id, out int currentProductCountForStore); // Defaults to 0 if key not found

                        int productsToCreateForThisStore = targetProductsPerStore - currentProductCountForStore;

                        logger.LogDebug("Store ID {StoreId} ('{StoreName}') - Current Products: {CurrentCount}, Target: {TargetCount}, Need to create: {NeedToCreate}",
                                        store.id, store.name, currentProductCountForStore, targetProductsPerStore, productsToCreateForThisStore);

                        if (productsToCreateForThisStore <= 0)
                        {
                            logger.LogDebug("Skipping Store ID {StoreId} as it already has enough products.", store.id);
                            continue; // Skip this store
                        }

                        // Find relevant product categories for this store
                        if (storeToProductCategoryMap.TryGetValue(store.category.name, out var relevantProductCategoryNames))
                        {
                            var relevantProductCategoryIds = relevantProductCategoryNames
                                .Select(name => productCategoriesLookup.TryGetValue(name, out int id) ? (int?)id : null)
                                .Where(id => id.HasValue).Select(id => id.Value).ToList();

                            if (!relevantProductCategoryIds.Any())
                            {
                                logger.LogWarning("No relevant Product Category IDs found for Store Category '{StoreCategoryName}'. Skipping products for Store ID {StoreId}.", store.category.name, store.id);
                                continue;
                            }

                            // Seed the required number of products *for this store*
                            for (int i = 0; i < productsToCreateForThisStore; i++)
                            {
                                int randomRelevantCategoryId = relevantProductCategoryIds[random.Next(relevantProductCategoryIds.Count)];
                                var productCategoryName = productCategoriesLookup.FirstOrDefault(kvp => kvp.Value == randomRelevantCategoryId).Key ?? "Proizvod";

                                // Generate unique name using a running counter
                                productNamingCounter++;
                                string productName = $"{productCategoryName} Artikl {productNamingCounter}";
                                decimal retailPrice = Math.Round((decimal)(random.NextDouble() * 50 + 1), 2);
                                decimal wholesalePrice; int? wholesaleThreshold = null;
                                if (random.Next(2) == 0) { wholesaleThreshold = random.Next(5, 21); wholesalePrice = Math.Round(retailPrice * (decimal)(random.NextDouble() * 0.1 + 0.8), 2); }
                                else { wholesalePrice = retailPrice; }
                                decimal? weight = null; string? weightUnit = null; decimal? volume = null; string? volumeUnit = null;
                                if (random.Next(2) == 0) { weight = Math.Round((decimal)(random.NextDouble() * 1.5 + 0.1), 3); weightUnit = "kg"; }
                                else { volume = Math.Round((decimal)(random.NextDouble() * 1.8 + 0.2), 3); volumeUnit = "l"; }
                                bool isActive = random.Next(10) < 9;

                                // Add the product using the helper (it checks for name+storeId collision)
                                await TrySeedProductAsync(catalogDbContext, logger,
                                    name: productName, categoryId: randomRelevantCategoryId, storeId: store.id,
                                    retailPrice: retailPrice, wholesalePrice: wholesalePrice, wholesaleThreshold: wholesaleThreshold,
                                    weight: weight, weightUnit: weightUnit, volume: volume, volumeUnit: volumeUnit, isActive: isActive);
                            }
                        }
                        else
                        {
                            logger.LogWarning("No Product Category mapping found for Store Category '{StoreCategoryName}'. Skipping products for Store ID {StoreId}.", store.category.name, store.id);
                        }
                    } // End foreach store

                    // --- 4. Save All Newly Added Products ---
                    var changes = await catalogDbContext.SaveChangesAsync();
                    if (changes > 0)
                    {
                        logger.LogInformation("Successfully saved {Count} new entities (Products and Pictures) to the database.", changes);
                    }
                    else
                    {
                        logger.LogInformation("No new Products needed seeding in this run (targets met or no suitable stores/categories).");
                    }

                    logger.LogInformation("Product seeding completed.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while seeding the Product data.");
                }
            }
        }

        // Helper method to seed a Product Category if it doesn't exist by name
        private static async Task<ProductCategory?> SeedProductCategoryAsync(CatalogDbContext context, ILogger logger, string categoryName)
        {
            try
            {
                var existingCategory = await context.ProductCategories.FirstOrDefaultAsync(c => c.Name.ToLower() == categoryName.ToLower());
                if (existingCategory == null)
                {
                    logger.LogInformation("Creating Product Category: {CategoryName}", categoryName);
                    var category = new ProductCategory { Name = categoryName };
                    await context.ProductCategories.AddAsync(category);
                    return category;
                }
                else
                {
                    logger.LogInformation("Product Category {CategoryName} already exists. Skipping creation.", categoryName);
                    return existingCategory;
                }
            }
            catch (Exception ex) { logger.LogError(ex, "Error seeding Product Category {CategoryName}", categoryName); return null; }
        }

        // Helper method to seed a Product if it doesn't exist by name and storeId
        // Returns true if a product was added to the context, false otherwise.
        private static async Task<bool> TrySeedProductAsync(
             CatalogDbContext context, ILogger logger, string name, int categoryId, int storeId,
            decimal retailPrice, decimal wholesalePrice, int? wholesaleThreshold,
            decimal? weight, string? weightUnit, decimal? volume, string? volumeUnit, bool isActive)
        {
            bool exists = await context.Products.AsNoTracking().AnyAsync(p => p.Name == name && p.StoreId == storeId);
            if (!exists)
            {
                var category = await context.ProductCategories.FindAsync(categoryId);
                if (category == null) { logger.LogWarning("Category ID {CategoryId} not found for Product '{ProductName}'. Skipping.", categoryId, name); return false; }

                // *** Generate random CreatedAt time ***
                // We'll use a similar range as stores for simplicity, could be refined
                DateTime randomCreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 366 * 10)).AddHours(-Random.Shared.Next(0, 24)); // Randomly within the last 10 years

                logger.LogInformation("Creating Product: {ProductName} in Store ID {StoreId}", name, storeId);
                var product = new Product
                {
                    Name = name,
                    ProductCategoryId = categoryId,
                    ProductCategory = category,
                    StoreId = storeId,
                    RetailPrice = retailPrice,
                    WholesalePrice = wholesalePrice,
                    WholesaleThreshold = wholesaleThreshold,
                    Weight = weight,
                    WeightUnit = weightUnit,
                    Volume = volume,
                    VolumeUnit = volumeUnit,
                    IsActive = isActive,
                    CreatedAt = randomCreatedAt // <-- Use the random date
                };
                int pictureCount = Random.Shared.Next(1, 4);
                for (int i = 0; i < pictureCount; i++)
                {
                    product.Pictures.Add(new ProductPicture { Url = $"https://via.placeholder.com/300x300/{GetRandomHexColor()}/FFFFFF?text={Uri.EscapeDataString(name)}+{i + 1}", Product = product });
                }
                await context.Products.AddAsync(product);
                return true; // Indicate product was added
            }
            else
            {
                logger.LogInformation("Product {ProductName} in Store ID {StoreId} already exists. Skipping creation.", name, storeId);
                return false; // Indicate product was skipped
            }
        }

        // Helper to define the Store Category -> Product Category mapping
        private static Dictionary<string, List<string>> GetStoreToProductCategoryMap()
        {
            return new Dictionary<string, List<string>>
            { /* ... Same as before ... */
                ["Prehrambene namirnice"] = new List<string> { "Voće", "Povrće", "Mliječni proizvodi", "Meso i Mesne prerađevine", "Pića", "Slatkiši i Grickalice", "Pekarski proizvodi", "Lična Higijena" },
                ["Elektronika"] = new List<string> { "Računari i Oprema", "Mobiteli i Oprema", "TV i Audio", "Mali Kućanski Aparati" },
                ["Odjeća i Obuća"] = new List<string> { "Odjeća", "Obuća" },
                ["Apoteka"] = new List<string> { "Lijekovi bez recepta", "Vitamini i Suplementi", "Medicinska Kozmetika", "Lična Higijena" },
                ["Željezarija"] = new List<string> { "Alati", "Boje i Lakovi", "Vrtni Program" },
                ["Kiosk / Trafika"] = new List<string> { "Novine i Časopisi", "Cigarete i Duhan", "Dopune za mobitel", "Slatkiši i Grickalice", "Pića" }
            };
        }
        private static string GetRandomHexColor() => Random.Shared.Next(0x1000000).ToString("X6");
    }

    public static class OrderDataSeeder
    {
        // Method to seed Orders and OrderItems
        public static async Task SeedOrdersAsync(this IHost host)
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
                var ordersDbContext = serviceProvider.GetRequiredService<OrdersDbContext>();
                var catalogDbContext = serviceProvider.GetRequiredService<CatalogDbContext>();
                var storeDbContext = serviceProvider.GetRequiredService<StoreDbContext>();
                var userManager = serviceProvider.GetRequiredService<UserManager<User>>(); // To get Buyers
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>(); // Consistent logger type

                try
                {
                    logger.LogInformation("Applying migrations for Orders database...");
                    await ordersDbContext.Database.MigrateAsync();
                    // Ensure other DBs are migrated if not done elsewhere
                    await catalogDbContext.Database.MigrateAsync();
                    await storeDbContext.Database.MigrateAsync();


                    logger.LogInformation("Seeding Orders and OrderItems...");

                    // --- 1. Check Existing Order Count ---
                    const int targetOrderCount = 35; // Define the desired total number
                    var currentOrderCount = await ordersDbContext.Orders.CountAsync();
                    logger.LogInformation("Current number of orders in DB: {CurrentCount}. Target: {TargetCount}", currentOrderCount, targetOrderCount);

                    int ordersToCreate = targetOrderCount - currentOrderCount;

                    if (ordersToCreate <= 0)
                    {
                        logger.LogInformation("Database already contains {CurrentCount} orders (target is {TargetCount}). No new orders will be seeded.", currentOrderCount, targetOrderCount);
                        return; // Exit early, we have enough or more orders
                    }

                    logger.LogInformation("Need to seed {OrdersToCreate} additional Orders to reach the target of {TargetCount}...", ordersToCreate, targetOrderCount);


                    // --- 2. Fetch Prerequisite Data ---
                    var buyers = await userManager.GetUsersInRoleAsync("Buyer");
                    var buyerIds = buyers.Select(b => b.Id).ToList();
                    if (!buyerIds.Any())
                    {
                        logger.LogWarning("No users with the 'Buyer' role found. Cannot seed Orders.");
                        return;
                    }

                    var storeIds = await storeDbContext.Stores.Select(s => s.id).ToListAsync();
                    if (!storeIds.Any())
                    {
                        logger.LogWarning("No Stores found. Cannot seed Orders.");
                        return;
                    }

                    // Fetch Products grouped by their StoreId for efficient lookup
                    var productsByStore = await catalogDbContext.Products
                                               .Where(p => p.IsActive) // Only consider active products
                                               .AsNoTracking()
                                               .GroupBy(p => p.StoreId)
                                               .ToDictionaryAsync(g => g.Key, g => g.ToList());

                    if (!productsByStore.Any())
                    {
                        logger.LogWarning("No active Products found in any store. Cannot seed Orders with items.");
                        // Decide if you want to seed orders without items or stop
                        return;
                    }


                    // --- 3. Seed Required Number of Orders ---
                    var random = Random.Shared;
                    var allStatuses = Enum.GetValues<OrderStatus>();

                    for (int i = 1; i <= ordersToCreate; i++)
                    {
                        // Select random buyer and store
                        string randomBuyerId = buyerIds[random.Next(buyerIds.Count)];
                        int randomStoreId = storeIds[random.Next(storeIds.Count)];

                        // Check if this store actually has products we fetched
                        if (!productsByStore.TryGetValue(randomStoreId, out var availableProductsForStore) || !availableProductsForStore.Any())
                        {
                            logger.LogDebug("Skipping order creation for Store ID {StoreId} as it has no active products.", randomStoreId);
                            continue; // Skip to the next order iteration
                        }

                        // Create the Order Header
                        var order = new OrderModel
                        {
                            BuyerId = randomBuyerId,
                            StoreId = randomStoreId,
                            // Assign a random status, maybe bias towards common ones
                            Status = allStatuses[random.Next(allStatuses.Length)],
                            Time = DateTime.UtcNow.AddDays(-random.Next(1, 90)).AddHours(-random.Next(0, 24)), // Random time in the past 90 days
                        };

                        // Add OrderItems
                        int numberOfItems = random.Next(1, 6); // 1 to 5 items per order
                        decimal calculatedTotal = 0m;

                        for (int j = 0; j < numberOfItems; j++)
                        {
                            // Select a random product *from this store's available products*
                            Product product = availableProductsForStore[random.Next(availableProductsForStore.Count)];
                            int quantity = random.Next(1, 11); // 1 to 10 quantity

                            // Determine Price (Retail vs Wholesale)
                            decimal itemPrice = product.RetailPrice;
                            if (product.WholesaleThreshold.HasValue && product.WholesalePrice.HasValue && quantity >= product.WholesaleThreshold.Value)
                            {
                                itemPrice = product.WholesalePrice.Value;
                            }

                            var orderItem = new OrderItem
                            {
                                Order = order, // Link back to parent order (EF Core uses this)
                                ProductId = product.Id,
                                Quantity = quantity,
                                Price = itemPrice
                            };

                            order.OrderItems.Add(orderItem); // Add to the order's collection
                            calculatedTotal += itemPrice * quantity;
                        }

                        // Set the calculated total on the order
                        order.Total = calculatedTotal;

                        // Add the complete order (with items) to the context
                        await ordersDbContext.Orders.AddAsync(order);
                        logger.LogInformation("Prepared Order for Buyer {BuyerId}, Store {StoreId} with {ItemCount} items.", randomBuyerId, randomStoreId, order.OrderItems.Count);

                    } // End of orders loop

                    // --- 4. Save All Newly Added Orders ---
                    var changes = await ordersDbContext.SaveChangesAsync();
                    if (changes > 0)
                    {
                        logger.LogInformation("Successfully saved {Count} new entities (Orders and OrderItems) to the database.", changes);
                    }
                    else
                    {
                        logger.LogInformation("No new Orders needed seeding in this run (or SaveChanges failed).");
                    }

                    logger.LogInformation("Order seeding completed.");

                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while seeding the Order data.");
                }
            }
        }
    }
}



