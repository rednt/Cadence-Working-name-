using Cadence.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Cadence.Infrastructure.Persistence
{
    public sealed class CadenceDbContext : DbContext
    {
        public DbSet<TaskItem> Tasks => Set<TaskItem>();
        public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

        public CadenceDbContext(DbContextOptions<CadenceDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TaskItem>().ToTable("Tasks");
            modelBuilder.Entity<NotificationLog>().ToTable("NotificationLogs");
            modelBuilder.Entity<TaskItem>(entity =>
            {
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            });
        }
    }
}