using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RentalAI.Api.Data;
using RentalAI.Api.Modules.Auth;
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

app.MapAppHealthChecks();
app.MapAuthModule();

await MigrateDatabaseAsync(app);

app.Run();

static async Task MigrateDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    for (var attempt = 1; attempt <= 10; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            return;
        }
        catch (Exception ex) when (attempt < 10)
        {
            logger.LogWarning(ex, "Database not ready, retrying migration {Attempt}/10", attempt);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}
