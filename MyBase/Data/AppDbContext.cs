using Microsoft.EntityFrameworkCore;
using MyBase.Models; // gleich für die User-Klasse

namespace MyBase.Data {
    public class AppDbContext : DbContext {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {
        }

        public DbSet<User> Users { get; set; }
    }
}
