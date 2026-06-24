using ClientScout.Application.Common.Interfaces;
using ClientScout.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClientScout.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<JobLead> JobLeads => Set<JobLead>();
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
    public DbSet<OutreachCampaign> OutreachCampaigns => Set<OutreachCampaign>();
    public DbSet<OutreachLog> OutreachLogs => Set<OutreachLog>();
    public DbSet<UserbotSession> UserbotSessions => Set<UserbotSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>().HasKey(x => x.Id);
        modelBuilder.Entity<User>().Property(x => x.Id).ValueGeneratedNever(); // Telegram ID is passed manually

        // JobLead Unique Constraint
        modelBuilder.Entity<JobLead>()
            .HasIndex(x => new { x.SourceId, x.ExternalId })
            .IsUnique();

        // OutreachCampaign JSONB Configuration
        modelBuilder.Entity<OutreachCampaign>()
            .Property(x => x.TargetChatsJson)
            .HasColumnType("jsonb");
    }
}
