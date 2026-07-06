using System.Text;
using ClientScout.Application;
using ClientScout.Application.Leads;
using ClientScout.Application.Outreach;
using ClientScout.Application.Search;
using ClientScout.Api.Security;
using ClientScout.Infrastructure;
using ClientScout.Infrastructure.Persistence;
using ClientScout.Scrapers;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IO;
using System.Threading.RateLimiting;

Console.SetIn(StreamReader.Null); // Prevent WTelegramClient from blocking locally

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientApp", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
            return;
        }

        if (builder.Environment.IsDevelopment())
        {
            policy
                .WithOrigins("http://localhost:5173", "https://localhost:5173")
                .AllowAnyHeader()
                .AllowAnyMethod();
            return;
        }

        policy.SetIsOriginAllowed(_ => false).AllowAnyHeader().AllowAnyMethod();
    });
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("Auth", context => RateLimitPartition.GetFixedWindowLimiter(
        GetRateLimitPartitionKey(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.AddPolicy("Search", context => RateLimitPartition.GetFixedWindowLimiter(
        GetRateLimitPartitionKey(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.AddPolicy("Api", context => RateLimitPartition.GetFixedWindowLimiter(
        GetRateLimitPartitionKey(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

// Add Layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScrapers();
builder.Services.AddHostedService<ClientScout.Api.Services.TelegramBotHostedService>();

// Configure JWT Authentication (real tokens only — no more fake-token hack)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => ConfigureJwtOptions(builder, options));

builder.Services.AddAuthorization();

// Add Hangfire
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"))));
builder.Services.AddHangfireServer();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.TryAdd("X-Content-Type-Options", "nosniff");
    headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
    headers.TryAdd("Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https:; " +
        "font-src 'self' data:; " +
        "connect-src 'self' https: wss:; " +
        "frame-ancestors 'self' https://web.telegram.org https://*.telegram.org https://t.me");
    await next();
});

app.UseCors("ClientApp");
app.UseHttpsRedirection();
app.Environment.WebRootPath ??= Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(Path.Combine(app.Environment.WebRootPath, "uploads"));
app.UseStaticFiles();

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

// Hangfire Dashboard
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireDashboardAuthorizationFilter(app.Configuration)]
});

app.MapControllers();

// Auto-migrate Database
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        dbContext.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error migrating database");
    }

    dbContext.Database.ExecuteSqlRaw("""
        ALTER TABLE "OutreachCampaigns"
        ADD COLUMN IF NOT EXISTS "ScheduleMode" text NOT NULL DEFAULT 'allday',
        ADD COLUMN IF NOT EXISTS "ScheduleStartTime" text NULL,
        ADD COLUMN IF NOT EXISTS "ScheduleEndTime" text NULL,
        ADD COLUMN IF NOT EXISTS "TimezoneOffsetMinutes" integer NOT NULL DEFAULT 0;
        """);

    dbContext.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "SearchSettings" (
            "Id" uuid NOT NULL,
            "ProfileId" uuid NOT NULL,
            "IsEnabled" boolean NOT NULL DEFAULT false,
            "NotificationsEnabled" boolean NOT NULL DEFAULT true,
            "IntervalMinutes" integer NOT NULL DEFAULT 30,
            "UserKeywords" text[] NOT NULL DEFAULT ARRAY[]::text[],
            "NegativeKeywords" text[] NOT NULL DEFAULT ARRAY[]::text[],
            "SearchProfileSummary" text NULL,
            "MustIncludeSignals" text[] NOT NULL DEFAULT ARRAY[]::text[],
            "SoftSignals" text[] NOT NULL DEFAULT ARRAY[]::text[],
            "RejectSignals" text[] NOT NULL DEFAULT ARRAY[]::text[],
            "ExpandedPositiveTerms" text[] NOT NULL DEFAULT ARRAY[]::text[],
            "ExpandedIntentTerms" text[] NOT NULL DEFAULT ARRAY[]::text[],
            "StrongTerms" text[] NOT NULL DEFAULT ARRAY[]::text[],
            "NeedsAiExpansion" boolean NOT NULL DEFAULT false,
            "LastAiExpandedAt" timestamp with time zone NULL,
            "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp with time zone NOT NULL DEFAULT now(),
            CONSTRAINT "PK_SearchSettings" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_SearchSettings_Profiles_ProfileId" FOREIGN KEY ("ProfileId") REFERENCES "Profiles" ("Id") ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_SearchSettings_ProfileId" ON "SearchSettings" ("ProfileId");
        """);

    dbContext.Database.ExecuteSqlRaw("""
        ALTER TABLE "SearchSettings"
        ADD COLUMN IF NOT EXISTS "SearchProfileSummary" text NULL,
        ADD COLUMN IF NOT EXISTS "MustIncludeSignals" text[] NOT NULL DEFAULT ARRAY[]::text[],
        ADD COLUMN IF NOT EXISTS "SoftSignals" text[] NOT NULL DEFAULT ARRAY[]::text[],
        ADD COLUMN IF NOT EXISTS "RejectSignals" text[] NOT NULL DEFAULT ARRAY[]::text[];
        """);

    dbContext.Database.ExecuteSqlRaw("""
        ALTER TABLE "JobLeads"
        ADD COLUMN IF NOT EXISTS "SourceType" integer NOT NULL DEFAULT 0,
        ADD COLUMN IF NOT EXISTS "SourceName" text NOT NULL DEFAULT '',
        ADD COLUMN IF NOT EXISTS "MatchedTerms" text[] NOT NULL DEFAULT ARRAY[]::text[],
        ADD COLUMN IF NOT EXISTS "Score" integer NOT NULL DEFAULT 0,
        ADD COLUMN IF NOT EXISTS "AiConfidence" integer NULL,
        ADD COLUMN IF NOT EXISTS "AiSummary" text NULL,
        ADD COLUMN IF NOT EXISTS "AiCategory" text NULL,
        ADD COLUMN IF NOT EXISTS "AiReason" text NULL,
        ADD COLUMN IF NOT EXISTS "AiStatus" integer NOT NULL DEFAULT 0,
        ADD COLUMN IF NOT EXISTS "ExpiresAt" timestamp with time zone NOT NULL DEFAULT (now() + interval '24 hours');
        CREATE INDEX IF NOT EXISTS "IX_JobLeads_ProfileId_FoundAt" ON "JobLeads" ("ProfileId", "FoundAt");
        """);

    dbContext.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "ExchangeConnections" (
            "Id" uuid NOT NULL,
            "ProfileId" uuid NOT NULL,
            "ExchangeType" integer NOT NULL DEFAULT 0,
            "IsConnected" boolean NOT NULL DEFAULT false,
            "RequiresReconnect" boolean NOT NULL DEFAULT false,
            "EncryptedSession" text NULL,
            "LastError" text NULL,
            "LastCheckedAt" timestamp with time zone NULL,
            "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
            "UpdatedAt" timestamp with time zone NOT NULL DEFAULT now(),
            CONSTRAINT "PK_ExchangeConnections" PRIMARY KEY ("Id"),
            CONSTRAINT "FK_ExchangeConnections_Profiles_ProfileId" FOREIGN KEY ("ProfileId") REFERENCES "Profiles" ("Id") ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_ExchangeConnections_ProfileId_ExchangeType" ON "ExchangeConnections" ("ProfileId", "ExchangeType");
        """);
}

// Schedule Background Jobs
using (var scope = app.Services.CreateScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    recurringJobManager.AddOrUpdate<ISearchJobService>(
        "SearchScheduler",
        service => service.ScheduleDueSearchAsync(CancellationToken.None),
        "* * * * *");

    recurringJobManager.AddOrUpdate<ISearchSettingsService>(
        "SearchProfileExpansionRecovery",
        service => service.EnqueuePendingProfileExpansionsAsync(CancellationToken.None),
        "* * * * *");

    recurringJobManager.AddOrUpdate<ISearchIngestionService>(
        "SearchLeadCleanup",
        service => service.CleanupExpiredLeadsAsync(CancellationToken.None),
        "*/5 * * * *");

    recurringJobManager.AddOrUpdate<ISearchIngestionService>(
        "SearchLeadAiRetry",
        service => service.ReclassifyUnverifiedLeadsAsync(CancellationToken.None),
        "* * * * *");

    recurringJobManager.AddOrUpdate<OutreachJobService>(
        "OutreachJob",
        service => service.ProcessCampaignsAsync(CancellationToken.None),
        "* * * * *");
}

app.Run();

void ConfigureJwtOptions(WebApplicationBuilder webApplicationBuilder, JwtBearerOptions jwtBearerOptions)
{
    var secret = GetRequiredJwtSecret(webApplicationBuilder.Configuration, webApplicationBuilder.Environment);
    jwtBearerOptions.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = webApplicationBuilder.Configuration["Jwt:Issuer"],
        ValidAudience = webApplicationBuilder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
    };
}

string GetRateLimitPartitionKey(HttpContext context)
{
    var accountId = AdminAccess.GetAccountId(context.User);
    return accountId?.ToString()
           ?? context.Connection.RemoteIpAddress?.ToString()
           ?? "anonymous";
}

static string GetRequiredJwtSecret(IConfiguration configuration, IWebHostEnvironment environment)
{
    var secret = configuration["Jwt:Secret"];
    if (string.IsNullOrWhiteSpace(secret))
    {
        throw new InvalidOperationException("Jwt:Secret must be configured from environment, user-secrets, or production secret storage.");
    }

    if (Encoding.UTF8.GetByteCount(secret) < 32)
    {
        throw new InvalidOperationException("Jwt:Secret must be at least 32 bytes.");
    }

    if (!environment.IsDevelopment() &&
        secret.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Jwt:Secret placeholder cannot be used outside Development.");
    }

    return secret;
}
