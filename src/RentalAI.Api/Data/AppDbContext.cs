using Microsoft.EntityFrameworkCore;
using RentalAI.Api.Modules.Auth;
using RentalAI.Api.Modules.Booking;
using RentalAI.Api.Modules.Kyc;
using RentalAI.Api.Modules.Notifications;
using RentalAI.Api.Modules.Properties;
using RentalAI.Api.Modules.Users;

namespace RentalAI.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<PropertyPhoto> PropertyPhotos => Set<PropertyPhoto>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<KycVerification> KycVerifications => Set<KycVerification>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
