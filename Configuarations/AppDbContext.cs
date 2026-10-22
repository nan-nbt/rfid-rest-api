using Microsoft.EntityFrameworkCore;
using RFIDAPI.Models;

namespace RFIDAPI.Configurations;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<SampleBarcode> SampleBarcodes { get; set; } = null!;
}
