using Microsoft.EntityFrameworkCore;

namespace WebKitchen.Services
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Define your DbSets (tables) here
        public DbSet<UserAccount> YourEntities { get; set; } // Example table
    }
}