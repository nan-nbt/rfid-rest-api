using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

/**
 * Authentication services
 * @function {AddAuthentication, AddJwtBearer}
 */
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["IamConfig:Authority"];
        options.Audience = builder.Configuration["IamConfig:Audience"];

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

/**
 * Database connection service
 * @function {AddDbContextFactory}
 */
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("PostgresConnection")));

/**
 * Controllers service👇
 * @function {AddControllers}
 */
builder.Services.AddControllers();

/**
 * Singleton Service
 * @function {AddSingleton}
 */
builder.Services.AddSingleton<RFIDConnectionManager>();

/**
 * Optional: Enable OpenAPI service
 * @function {AddOpenApi}
 */
builder.Services.AddOpenApi();

/**
 * Build application
 * @function {Build}
 * @includes {MapOpenApi, UseHttpsRedirection, UseAuthentication, UseAuthorization, MapControllers, Run}
 * @return {WebApplication}
 */
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Enable HTTPS
app.UseHttpsRedirection();

// Enable Authentication and Authorization Middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

