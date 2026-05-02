using Accurat.WebAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace Accurat.WebAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Branch> Branches => Set<Branch>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<Service> Services => Set<Service>();
        public DbSet<Client> Clients => Set<Client>();
        public DbSet<Shift> Shifts => Set<Shift>();
        public DbSet<Transaction> Transactions => Set<Transaction>();
        public DbSet<EmployeeScheduleEntry> EmployeeSchedules => Set<EmployeeScheduleEntry>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Добавляем дефолтный филиал, если база пустая
            modelBuilder.Entity<Branch>().HasData(
                new Branch
                {
                    Id = 1,
                    Name = "ACCURAT - Девятый м-н",
                    Address = "ул. Строителей, 54",
                    Type = 1
                }
            );

            modelBuilder.Entity<EmployeeScheduleEntry>()
                .HasKey(e => new { e.EmployeeId, e.Year, e.Month, e.Day });

            // Здесь мы можем прописать хитрые правила, если понадобятся,
            // но EF Core 8 настолько умный, что сам поймет почти все связи!
        }
    }
}