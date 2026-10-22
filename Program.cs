using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using RFIDAPI.Configurations;
using RFIDAPI.Managers;

namespace RFIDAPI;

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        /**
         * IAM services
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

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var realmAccessClaim = context.Principal?.FindFirst("realm_access");
                        if (realmAccessClaim != null)
                        {
                            using var jsonDoc = System.Text.Json.JsonDocument.Parse(realmAccessClaim.Value);
                            if (jsonDoc.RootElement.TryGetProperty("roles", out var rolesElement))
                            {
                                var identity = context.Principal?.Identity as ClaimsIdentity;

                                if (identity != null)
                                {
                                    foreach (var role in rolesElement.EnumerateArray())
                                    {
                                        if (role.GetString() is { } roleName)
                                        {
                                            identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
                                        }
                                    }
                                }
                            }
                        }
                        return Task.CompletedTask;
                    }
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
        builder.Services.AddSingleton<RfidConnectionManager>();

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
    }
}
