using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.MySql;
using Microsoft.EntityFrameworkCore;
using RentalAI.Api.Data;
using RentalAI.Api.Hosting;
using RentalAI.Api.Modules.Auth;
using RentalAI.Api.Modules.Booking;
using RentalAI.Api.Modules.Dashboard;
using RentalAI.Api.Modules.Files;
using RentalAI.Api.Modules.Kyc;
using RentalAI.Api.Modules.Notifications;
using RentalAI.Api.Modules.Properties;
using RentalAI.Api.Modules.Users;
using RentalAI.Common.Logging;
using RentalAI.Common.Web;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.UseAppSerilog();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        MySqlConnectionString.Build(builder.Configuration),
        new MySqlServerVersion(new Version(8, 4, 0))));

var jwtOptions = JwtOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(jwtOptions);

builder.Services.AddAuthModule(jwtOptions);
builder.Services.AddAuthorization();
builder.Services.AddAppRateLimiting();

builder.Services.AddFilesModule(builder.Configuration);
builder.Services.AddPropertiesModule();
builder.Services.AddBookingModule(builder.Configuration);
builder.Services.AddKycModule();
builder.Services.AddUsersModule();
builder.Services.AddDashboardModule();
builder.Services.AddNotificationsModule(builder.Configuration);

builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseStorage(new MySqlStorage(
        MySqlConnectionString.BuildForHangfire(builder.Configuration),
        new MySqlStorageOptions { TablesPrefix = "hangfire_" })));
builder.Services.AddHangfireServer();

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(tags: [HealthCheckExtensions.ReadyTag]);

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization =
    [
        new HangfireDashboardAuthFilter(
            builder.Configuration["HANGFIRE_DASHBOARD_USER"] ?? "admin",
            builder.Configuration["HANGFIRE_DASHBOARD_PASSWORD"] ?? "admin")
    ]
});

app.MapAppHealthChecks();
app.MapAuthModule();
app.MapPropertiesModule();
app.MapBookingModule();
app.MapKycModule();
app.MapUsersModule();
app.MapDashboardModule();
app.MapNotificationsModule();

await StartupAsync(app);

app.Run();

static async Task StartupAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var storage = scope.ServiceProvider.GetRequiredService<FileStorage>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    for (var attempt = 1; attempt <= 10; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            break;
        }
        catch (Exception ex) when (attempt < 10)
        {
            logger.LogWarning(ex, "Database not ready, retrying migration {Attempt}/10", attempt);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }

    await storage.EnsureBucketsAsync(CancellationToken.None);
}
