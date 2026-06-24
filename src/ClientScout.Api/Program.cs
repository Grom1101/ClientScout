using System.Text;
using ClientScout.Application;
using ClientScout.Application.Leads;
using ClientScout.Application.Outreach;
using ClientScout.Infrastructure;
using ClientScout.Infrastructure.Persistence;
using ClientScout.Scrapers;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Add Layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScrapers();

// Configure JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { ConfigureOptions(builder, options); });

builder.Services.AddAuthorization();

// Add Hangfire
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"))));
builder.Services.AddHangfireServer();

var app = builder.Build();

app.UseCors(x => x.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Hangfire Dashboard (MVP: accessible without auth, should secure in production)
app.UseHangfireDashboard("/hangfire");

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
        Console.WriteLine($"Error migrating database: {ex.Message}");
    }
}

// Schedule Background Parsing Job
using (var scope = app.Services.CreateScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    // Run Parsing every 10 minutes
    recurringJobManager.AddOrUpdate<LeadParsingService>(
        "ScrapeJob",
        service => service.RunParsingJobAsync(CancellationToken.None),
        "*/10 * * * *");

    // Run Outreach every minute
    recurringJobManager.AddOrUpdate<OutreachJobService>(
        "OutreachJob",
        service => service.ProcessCampaignsAsync(CancellationToken.None),
        "* * * * *");
}

app.Run();

void ConfigureOptions(WebApplicationBuilder? webApplicationBuilder, JwtBearerOptions jwtBearerOptions)
{
    var secret = webApplicationBuilder.Configuration["Jwt:Secret"] ??
                 "A_VERY_LONG_SECRET_KEY_THAT_NEEDS_TO_BE_AT_LEAST_32_BYTES_LONG_OR_MORE";
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
