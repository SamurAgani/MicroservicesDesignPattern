using Microsoft.EntityFrameworkCore;

namespace StockAPI.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {

        }
        public DbSet<Stock> stocks { get; set; }
    }
}
