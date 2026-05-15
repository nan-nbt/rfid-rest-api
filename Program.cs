using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Tambahkan ini 👇
builder.Services.AddControllers();

// builder.Services.AddDbContext<AppDbContext>(options =>
//     options.UseNpgsql(
//         builder.Configuration.GetConnectionString("PostgresConnection")));

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("PostgresConnection")));

// (opsional, kalau masih mau OpenAPI)
builder.Services.AddOpenApi();

// builder.Services.AddScoped<UHFService>();

// Singleton
builder.Services.AddSingleton<RFIDConnectionManager>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Tambahkan ini 👇 (PENTING)
app.MapControllers();

app.Run();