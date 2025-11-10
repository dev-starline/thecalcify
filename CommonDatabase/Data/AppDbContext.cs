using CommonDatabase.DTO;
using CommonDatabase.Models;

using Microsoft.EntityFrameworkCore;

namespace CommonDatabase
{

    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<AdminLogin> AdminLogin { get; set; }

        public DbSet<ClientUser> Client { get; set; }

        public DbSet<Subscribe> Subscribe { get; set; }

        public DbSet<Instruments> Instruments { get; set; }

        public DbSet<NotificationAlert> NotificationAlerts { get; set; }

        public DbSet<SelfSubscribe> SelfSubscriber { get; set; }

        public DbSet<WatchInstrument> WatchInstrument { get; set; }
        public DbSet<ClientDevices> ClientDevices { get; set; }
        public DbSet<DeviceAccessRawDto> DeviceAccessRawDto { get; set; }
        public DbSet<ClientAccessModel> ClientAccessModel { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<InstrumentUserDto>().HasNoKey();
        }

    }
}
