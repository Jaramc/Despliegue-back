using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.MySql;
using Microsoft.AspNetCore.HttpOverrides;
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
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip;
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

var trustedProxyNetworks = (builder.Configuration["TRUSTED_PROXY_NETWORKS"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    foreach (var network in trustedProxyNetworks)
    {
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
    }
});

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

const string CorsPolicyName = "FrontendCors";
var corsAllowedOrigins = (builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
        policy.WithOrigins(corsAllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseRateLimiter();
app.UseCors(CorsPolicyName);
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
            builder.Configuration["HANGFIRE_DASHBOARD_USER"] ?? throw new InvalidOperationException("HANGFIRE_DASHBOARD_USER is not configured"),
            builder.Configuration["HANGFIRE_DASHBOARD_PASSWORD"] ?? throw new InvalidOperationException("HANGFIRE_DASHBOARD_PASSWORD is not configured"))
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
    await SeedOwnerAsync(app, scope);
}

static async Task SeedOwnerAsync(WebApplication app, IServiceScope scope)
{
    var email = app.Configuration["SEED_OWNER_EMAIL"];
    var password = app.Configuration["SEED_OWNER_PASSWORD"];
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
    {
        return;
    }

    var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
    await authService.EnsureOwnerAsync(email, password, CancellationToken.None);
}
