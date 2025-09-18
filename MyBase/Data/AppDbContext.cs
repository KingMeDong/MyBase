using Microsoft.EntityFrameworkCore;
using MyBase.Models;            // für User, PasswordEntry, usw.
using MyBase.Models.Finance;    // für Instrument, FeedState, AppSetting

namespace MyBase.Data {
    public class AppDbContext : DbContext {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<MyBase.Models.PasswordEntry> Passwords { get; set; }
        public DbSet<CalendarEvent> CalendarEvents { get; set; }
        public DbSet<SmartDevice> SmartDevices { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<CodeRepository> CodeRepositories { get; set; }
        public DbSet<CodeSnapshot> CodeSnapshots { get; set; }

        // --- Finance/MarketData ---
        public DbSet<AppSetting> AppSettings { get; set; }
        public DbSet<FeedState> FeedStates { get; set; }
        public DbSet<Instrument> Instruments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<FeedState>()
                .HasOne(fs => fs.Instrument)
                .WithOne()
                .HasForeignKey<FeedState>(fs => fs.InstrumentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Instrument>()
                .HasIndex(i => i.Symbol)
                .HasDatabaseName("IX_Instrument_Symbol");
        }
    }
}
