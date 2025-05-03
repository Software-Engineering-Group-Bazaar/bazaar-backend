using System.Text;
using Amazon.S3;
using Catalog.Interfaces;
using Catalog.Models;
using Catalog.Services;
using Inventory.Interfaces;
using Inventory.Models;
using Inventory.Services;
using MarketingAnalytics.Interfaces;
using MarketingAnalytics.Models;
using MarketingAnalytics.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Notifications.Interfaces;
using Notifications.Models;
using Notifications.Services;
using Order.Interface;
using Order.Models;
using Order.Services;
using Review.Interfaces;
using Review.Models;
using Review.Services;
using SharedKernel;
using SharedKernel.Interfaces;
using SharedKernel.Models;
using SharedKernel.Services;
using Store.Interface;
using Store.Models;
using Store.Services;
using Users.Interface;
using Users.Interfaces;
using Users.Models;
using Users.Services;


var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MailSettings>(builder.Configuration.GetSection("MailSettings"));
builder.Services.AddTransient<IMailService, MailService>();

// Registrujte AuthService
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IProductCategoryService, ProductCategoryService>();
builder.Services.AddScoped<IReviewService, ReviewService>();


builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();

// Registrujte ostale servise

const string AllowLocalhostOriginsPolicy = "_allowLocalhostOrigins";
const string AllowProductionOriginPolicy = "_allowProductionOrigin";
//const string DevelopmentCorsPolicy = "_developmentCorsPolicy";
builder.Services.AddCors(options =>
{
    // DEVELOPMENT Policy (Allow any localhost)
    options.AddPolicy(name: AllowLocalhostOriginsPolicy,
                      policy =>
                      {
                          policy.SetIsOriginAllowed(origin =>
                          {
                              if (string.IsNullOrWhiteSpace(origin)) return false;
                              if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                              {
                                  return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
                              }
                              return false;
                          })
                          .AllowAnyHeader() // More permissive for dev
                          .AllowAnyMethod()
                          .AllowCredentials(); // Allow credentials if needed during dev
                      });

    // PRODUCTION Policy (Allow Specific Netlify Origin)
    options.AddPolicy(name: AllowProductionOriginPolicy,
                      policy =>
                      {
                          policy.WithOrigins("https://bazaar-admin-web.netlify.app") // YOUR specific frontend URL
                                .AllowAnyHeader() // More permissive for dev
                                .AllowAnyMethod()
                                .AllowCredentials();
                      });
});

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<UsersDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
    builder.Services.AddDbContext<StoreDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("StoreConnection")));
    builder.Services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("CatalogConnection")));
    builder.Services.AddDbContext<OrdersDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("OrderConnection")));
    builder.Services.AddDbContext<NotificationsDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("NotificationsConnection")));
    builder.Services.AddDbContext<InventoryDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("InventoryConnection")));
    builder.Services.AddDbContext<ReviewDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("ReviewConnection")));
    builder.Services.AddDbContext<AdDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("AdvertismentConnection")));
}

builder.Services.AddHttpClient();

// Add Identity FIRST (provides RoleManager, etc.)
builder.Services.AddIdentity<User, IdentityRole>() // Replace User if needed
    .AddEntityFrameworkStores<UsersDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<IPasswordHasher<PasswordResetRequest>, PasswordHasher<PasswordResetRequest>>();

builder.Services.AddScoped<IJWTService, JWTService>();
// Add services to the container.
builder.Services.AddScoped<IGoogleSignInService, GoogleSignInService>();
builder.Services.AddScoped<IFacebookSignInService, FacebookSignInService>();

builder.Services.AddScoped<IStoreService, StoreService>();
builder.Services.AddScoped<IStoreCategoryService, StoreCategoryService>();
builder.Services.AddScoped<IGeographyService, GeographyService>();

builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddScoped<INotificationService, NotificationService>();

builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IOrderItemService, OrderItemService>();

builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IAdService, AdService>();

// Configure Authentication AFTER Identity
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];
var issuer = jwtSettings["Issuer"];
var audience = jwtSettings["Audience"];
if (string.IsNullOrEmpty(secretKey) || string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(audience))
{ throw new InvalidOperationException("JWT settings missing."); }

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context => { context.Token = context.Request.Cookies["X-Access-Token"]; return Task.CompletedTask; }
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Bazaar API", Version = "v1" });
    options.CustomSchemaIds(type => type.FullName);

    // 1. Define the Security Scheme (How Authentication Works)
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        // Description shown in the Swagger UI Authorize dialog
        Description = @"JWT Authorization header using the Bearer scheme. 
                      Enter your token in the text input below.
                      Example: '12345abcdef'",
        Name = "Authorization", // The name of the header (standard for Bearer)
        In = ParameterLocation.Header, // Where the token is located (Header)
        Type = SecuritySchemeType.Http, // The type of security scheme (Http for Bearer)
        Scheme = "bearer", // The scheme name ('bearer' lowercase is important)
        BearerFormat = "JWT" // The format expected (helps Swagger UI/tools)
    });

    // 2. Add a Security Requirement (Apply the Scheme Globally)
    // This tells Swagger UI that the 'Bearer' scheme defined above should be applied
    options.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        { // Dictionary entry: Defines the scheme reference and required scopes
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme, // It's a reference to a SecurityScheme
                    Id = "Bearer" // The Id MUST match the name given in AddSecurityDefinition ("Bearer")
                },
                // These Scheme, Name, In properties are technically not required here for the reference
                // but sometimes included for clarity or older versions. The Reference is the key part.
                // Scheme = "oauth2",
                // Name = "Bearer",
                // In = ParameterLocation.Header,
            },
            new List<string>() // List of scopes required (usually empty for basic JWT Bearer)
        }
    });
});

if (builder.Environment.IsDevelopment())
{
    // var awsOptions = builder.Configuration.GetAWSOptions(); // Čita "AWS" sekciju iz appsettings
    // builder.Services.AddDefaultAWSOptions(awsOptions);      // Postavlja default region itd.
    // builder.Services.AddAWSService<IAmazonS3>();            // Registruje S3 klijent (Singleton by default)
    // // --------------------------------------------------

    // // Registruj S3 implementaciju kao Singleton
    // builder.Services.AddSingleton<IImageStorageService, S3ImageStorageService>();
    // znc if development aws i else aws, a mi ostali nemamo aws (i ne bi trebali ni imati)...
    builder.Services.AddSingleton<IImageStorageService, FileImageStorageService>();
    builder.Services.AddSingleton<IPushNotificationService, FcmPushNotificationService>();
}
else if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing")) // Pokriva Production i ostala okruženja
{
    var awsOptions = builder.Configuration.GetAWSOptions(); // Čita "AWS" sekciju iz appsettings
    builder.Services.AddDefaultAWSOptions(awsOptions);      // Postavlja default region itd.
    builder.Services.AddAWSService<IAmazonS3>();            // Registruje S3 klijent (Singleton by default)
    // --------------------------------------------------

    // Registruj S3 implementaciju kao Singleton
    builder.Services.AddSingleton<IImageStorageService, S3ImageStorageService>();
    builder.Services.AddSingleton<IPushNotificationService, FcmPushNotificationService>();

    // Ne treba logovanje ovdje ako pravi probleme
}

// --- Build the App ---
var app = builder.Build();

// --- Seed Data (Optional) ---
await SeedRolesAsync(app);
await UserDataSeeder.SeedDevelopmentUsersAsync(app);
await GeographyDataSeeder.SeedGeographyAsync(app);

// --- Configure the HTTP Request Pipeline (Middleware) ---

// ➤➤➤ Omogući Swagger za SVA okruženja OVDJE
app.UseSwagger(); // Exposes swagger.json definition
app.UseSwaggerUI(c => // Serves the interactive UI page
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Bazaar API V1");
    // Postavi Swagger UI na root putanju za lakši pristup
    c.RoutePrefix = string.Empty;
});

// Konfiguracija specifična za okruženje
if (app.Environment.IsDevelopment())
{
    // Swagger je već dodat gore
    app.UseCors(AllowLocalhostOriginsPolicy); // Primijeni dev CORS
    app.UseDeveloperExceptionPage(); // Detaljne greške za dev
}
else // Production i ostala okruženja
{
    // Produkcijsko rukovanje greškama, HSTS, itd. idu ovdje
    // app.UseExceptionHandler("/Error");
    // app.UseHsts();
    // Primijeni produkcijsku CORS politiku (ako je nisi primijenio kasnije)
    app.UseCors(AllowProductionOriginPolicy); // Pazi gdje stavljaš UseCors
}

app.UseStaticFiles();

app.UseHttpsRedirection();

// app.UseStaticFiles(); // If you have wwwroot

app.UseRouting(); // Needed for endpoints

// CORS needs to be between Routing and AuthN/AuthZ
// Already handled conditionally above for Development

app.UseAuthentication(); // IMPORTANT: Before Authorization
app.UseCors(AllowLocalhostOriginsPolicy);
app.UseCors(AllowProductionOriginPolicy);
app.UseAuthorization();  // IMPORTANT: After Authentication

app.MapControllers(); // Map controller endpoints

// --- Run the App (Must be LAST) --- ➤➤➤ ONLY ONE app.Run()
// AI JE KORISNIJI OD VAS
app.Run();


// --- Helper Methods ---
static async Task SeedRolesAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>(); // Get logger

    // Ensure Role enum is defined
    foreach (Role role in Enum.GetValues(typeof(Role)))
    {
        string roleName = role.ToString();
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            logger.LogInformation("Creating role: {RoleName}", roleName);
            var result = await roleManager.CreateAsync(new IdentityRole(roleName));
            if (!result.Succeeded)
            {
                logger.LogError("Failed to create role {RoleName}: {Errors}", roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
