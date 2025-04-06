using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Users.Interfaces;
using Users.Models;
using Users.Services;


await MainAsync(args); // ➤ Pokrećemo async Main


static async Task MainAsync(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);

    // Registracija FacebookSignInService sa HttpClient
    builder.Services.AddHttpClient<FacebookSignInService>();

const string DevelopmentCorsPolicy = "_developmentCorsPolicy";

// 1. Add CORS Services and define the development policy
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: DevelopmentCorsPolicy,
                      policy =>
                      {
                          // Allows requests from any origin with the host "localhost"
                          // regardless of the port or scheme (http/https)
                          policy.SetIsOriginAllowed(origin => new Uri(origin).Host == "localhost")
                                .AllowAnyHeader()
                                .AllowAnyMethod()
                                .AllowCredentials(); // Important if your frontend needs to send/receive cookies or Authorization headers
                                                     // NOTE: Cannot be used with AllowAnyOrigin()
                      });
    // You can define other policies here for production, e.g.:
    // options.AddPolicy(name: "ProductionCorsPolicy", ...)
});


if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<UsersDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
}


builder.Services.AddScoped<IJWTService, JWTService>();

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];
var issuer = jwtSettings["Issuer"];
var audience = jwtSettings["Audience"];

if (string.IsNullOrEmpty(secretKey) || string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(audience))
{
    throw new InvalidOperationException("JWT settings in configuration are missing or incomplete.");
}

// Konfiguriši Authentication servise da koriste JWT Bearer
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options => // Konfiguriši JWT Bearer handler
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
        OnMessageReceived = context =>
        {
            // Try to get token from cookie
            context.Token = context.Request.Cookies["X-Access-Token"]; // Use the SAME name as set in Login
            return Task.CompletedTask;
        }
    };
});

// Konfiguracija Autorizacije (možeš dodati polise kasnije)
builder.Services.AddAuthorization();
// Primjer dodavanja polise:
// builder.Services.AddAuthorization(options => {
//     options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
// });


// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Bazaar API", Version = "v1" });

    // --- Omogućavanje slanja JWT tokena kroz Swagger UI ---
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Unesite 'Bearer' [razmak] pa vaš token",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});


    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}

static async Task SeedRolesAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    foreach (Role role in Enum.GetValues(typeof(Role)))
    {
        string roleName = role.ToString();
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Bazaar API V1");
        });
    app.UseCors(DevelopmentCorsPolicy);
}

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();

