using Halal_project_BL.Repositories;
using Halal_project_BL.Services;
using HalalProject.Database.Data;
using log4net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Configuration;
using System.Text;
using System.Text.Json.Serialization;




var builder = WebApplication.CreateBuilder(args);

    // Add service defaults & Aspire components.
    builder.AddServiceDefaults();
    builder.Services.AddLocalization();

    builder.Services.AddControllers();
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

// Register HttpClient for Google AI Studio API
builder.Services.AddHttpClient<IIngredientRepository, IngredientRepository>();

// Register repository and service
builder.Services.AddScoped<IIngredientRepository, IngredientRepository>();
builder.Services.AddScoped<IIngredientService, IngredientService>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        b => b.MigrationsAssembly("HalalProject.Database")));

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddSwaggerGen(c =>
    {
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            In = ParameterLocation.Header,
            Description = "Please insert JWT with Bearer into field",
            Name = "Authorization",
            Type = SecuritySchemeType.ApiKey
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
        {
            new OpenApiSecurityScheme
            {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
            },
            Array.Empty<string>()
        }
        });
    });


var secret = builder.Configuration.GetValue<string>("Jwt:Secret");
var issuer = builder.Configuration.GetValue<string>("Jwt:Issuer");
var audience = builder.Configuration.GetValue<string>("Jwt:Audience");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration.GetValue<string>("Jwt:Issuer"), // "MyAwesomeApp"
        ValidAudience = audience, // "MyAwesomeAudience"
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
    };
});

builder.Services.AddDistributedMemoryCache(); // Or your preferred cache implementation
builder.Services.AddAuthorization();


    builder.Services.AddScoped<IProductService, ProductService>();
    builder.Services.AddScoped<IProductRepository, ProductRepository>();
    builder.Services.AddScoped<IAuthRepository, AuthRepository>();
    builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IIngredientRepository, IngredientRepository>();
builder.Services.AddScoped<IIngredientService, IngredientService>();
builder.Services.AddScoped<IProductChangeRequestService, ProductChangeRequestService>();
builder.Services.AddScoped<IProductRequestRepository,ProductRequestRepository>();
builder.Services.AddScoped<IIngredientRepository, IngredientRepository>();
builder.Services.AddScoped<DataService>();
// In Program.cs or Startup.cs
builder.Services.AddControllers()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });
// Add services to the container.
builder.Services.AddProblemDetails();

    var app = builder.Build();


    // Configure the HTTP request pipeline.
    app.UseExceptionHandler();
    app.UseAuthentication();
    app.UseAuthorization();

    var supportedCultures = new[] { "en-US", "fr-FR" };
    var localizeoptions = new RequestLocalizationOptions()
        .SetDefaultCulture("en-US")
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);

    app.UseRequestLocalization(localizeoptions);
app.UseStaticFiles();
app.MapControllers();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.MapDefaultEndpoints();

    app.Run();

