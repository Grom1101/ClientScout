using ClientScout.Application.Common.Interfaces;
using ClientScout.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClientScout.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<ExchangeConnection> ExchangeConnections => Set<ExchangeConnection>();
    public DbSet<SearchSettings> SearchSettings => Set<SearchSettings>();
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<JobLead> JobLeads => Set<JobLead>();
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
    public DbSet<OutreachCampaign> OutreachCampaigns => Set<OutreachCampaign>();
    public DbSet<OutreachLog> OutreachLogs => Set<OutreachLog>();
    public DbSet<UserbotSession> UserbotSessions => Set<UserbotSession>();
    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Account
        modelBuilder.Entity<Account>().HasKey(x => x.Id);
        modelBuilder.Entity<Account>()
            .HasIndex(x => x.Email)
            .IsUnique();
        modelBuilder.Entity<Account>()
            .Ignore(x => x.IsTelegramLinked); // computed property, not stored

        // User (Telegram data)
        modelBuilder.Entity<User>().HasKey(x => x.Id);
        modelBuilder.Entity<User>().Property(x => x.Id).ValueGeneratedNever();

        // Profile: belongs to Account
        modelBuilder.Entity<Profile>()
            .HasOne(p => p.Account)
            .WithMany(a => a.Profiles)
            .HasForeignKey(p => p.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SearchSettings>()
            .HasOne(s => s.Profile)
            .WithOne(p => p.SearchSettings)
            .HasForeignKey<SearchSettings>(s => s.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SearchSettings>()
            .HasIndex(s => s.ProfileId)
            .IsUnique();

        modelBuilder.Entity<ExchangeConnection>()
            .HasOne(c => c.Profile)
            .WithMany(p => p.ExchangeConnections)
            .HasForeignKey(c => c.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ExchangeConnection>()
            .HasIndex(c => new { c.ProfileId, c.ExchangeType })
            .IsUnique();

        // JobLead Unique Constraint
        modelBuilder.Entity<JobLead>()
            .HasIndex(x => new { x.SourceId, x.ExternalId })
            .IsUnique();

        modelBuilder.Entity<JobLead>()
            .HasIndex(x => new { x.ProfileId, x.FoundAt });

        // OutreachCampaign JSONB Configuration
        modelBuilder.Entity<OutreachCampaign>()
            .Property(x => x.TargetChatsJson)
            .HasColumnType("jsonb");
    }
}
