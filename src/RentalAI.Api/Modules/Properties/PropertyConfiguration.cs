using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RentalAI.Api.Modules.Properties;

public sealed class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.ToTable("properties");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Title).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(4000).IsRequired();
        builder.Property(p => p.Address).HasMaxLength(300).IsRequired();
        builder.Property(p => p.City).HasMaxLength(120).IsRequired();
        builder.Property(p => p.Country).HasMaxLength(120).IsRequired();
        builder.Property(p => p.Latitude).HasPrecision(9, 6);
        builder.Property(p => p.Longitude).HasPrecision(9, 6);
        builder.Property(p => p.NightlyRate).HasPrecision(18, 2);
        builder.HasIndex(p => new { p.City, p.Country });
        builder.HasIndex(p => p.OwnerId);
        builder.HasMany(p => p.Photos)
            .WithOne()
            .HasForeignKey(photo => photo.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PropertyPhotoConfiguration : IEntityTypeConfiguration<PropertyPhoto>
{
    public void Configure(EntityTypeBuilder<PropertyPhoto> builder)
    {
        builder.ToTable("property_photos");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Url).HasMaxLength(600).IsRequired();
        builder.HasIndex(p => p.PropertyId);
    }
}
